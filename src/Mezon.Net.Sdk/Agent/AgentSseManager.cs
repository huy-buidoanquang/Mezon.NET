using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Agent
{
    public sealed class AgentSseSessionEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Long-lived Agent SSE client. Authentication matches mezon-sdk: <c>appid</c> and <c>token</c> query fields
    ///     on <c>api/sse/metadata</c>. The read loop rents buffers and allocates only the payload string handed to subscribers.
    /// </summary>
    public sealed class AgentSseManager : IDisposable
    {
        public const int DefaultReconnectDelayMs = 3000;
        public const int MaxReconnectDelayMs = 30000;

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly Uri _endpoint;
        private readonly int _maxReconnectAttempts;
        private readonly int _reconnectDelayMs;
        private readonly Random _jitter = new Random();
        private readonly object _gate = new object();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private int _disposed;

        public event Func<AgentSseSessionEvent, Task>? MessageReceived;

        public AgentSseManager(string baseUrl, long appId, string token, HttpClient? httpClient = null, int maxReconnectAttempts = 0, int reconnectDelayMs = DefaultReconnectDelayMs)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Agent event URL is required.", nameof(baseUrl));
            }

            if (appId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(appId));
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Agent token is required.", nameof(token));
            }

            if (reconnectDelayMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectDelayMs));
            }

            _endpoint = BuildEndpoint(baseUrl, appId, token);
            _maxReconnectAttempts = maxReconnectAttempts;
            _reconnectDelayMs = reconnectDelayMs;
            if (httpClient is null)
            {
                _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                _ownsHttp = true;
            }
            else
            {
                _http = httpClient;
            }
        }

        public static Uri BuildEndpoint(string baseUrl, long appId, string token)
        {
            var url = baseUrl.TrimEnd('/');
            var separator = url.IndexOf('?') >= 0 ? "&" : "?";
            var built = url + "/api/sse/metadata" + separator
                + "appid=" + Uri.EscapeDataString(appId.ToString())
                + "&token=" + Uri.EscapeDataString(token);
            return new Uri(built);
        }

        public static int CalculateReconnectDelayMs(int attempt, int baseDelayMs, int jitterMs)
        {
            if (attempt < 0)
            {
                attempt = 0;
            }

            if (jitterMs < 0)
            {
                jitterMs = 0;
            }

            var shift = Math.Min(attempt, 16);
            long scaled = (long)baseDelayMs << shift;
            if (scaled > MaxReconnectDelayMs || scaled <= 0)
            {
                scaled = MaxReconnectDelayMs;
            }

            return (int)scaled + jitterMs;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(AgentSseManager));
                }

                if (_loop is { IsCompleted: false })
                {
                    return Task.CompletedTask;
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _loop = Task.Run(() => RunAsync(_cts.Token));
                return Task.CompletedTask;
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var opened = false;
                try
                {
                    opened = await ReadOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    opened = false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (opened)
                {
                    attempt = 0;
                }

                if (_maxReconnectAttempts > 0 && attempt >= _maxReconnectAttempts)
                {
                    break;
                }

                int jitter;
                lock (_jitter)
                {
                    jitter = _jitter.Next(0, 1000);
                }

                var delay = CalculateReconnectDelayMs(attempt, _reconnectDelayMs, jitter);
                attempt++;
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task<bool> ReadOnceAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
            using var decoder = new AgentSseDecoder();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return true;
                    }

                    decoder.Push(buffer.AsSpan(0, read), Dispatch);
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        private void Dispatch(ReadOnlyMemory<byte> payload)
        {
            var handler = MessageReceived;
            if (handler is null || payload.Length == 0)
            {
                return;
            }

            var bytes = payload.Span;
            Span<char> typeBuffer = stackalloc char[64];
            var eventType = AgentSseDecoder.TryReadEventType(bytes, typeBuffer, out var written)
                ? new string(typeBuffer.Slice(0, written))
                : string.Empty;
            var raw = Encoding.UTF8.GetString(bytes);
            var evt = new AgentSseSessionEvent
            {
                EventType = eventType,
                RawResponse = raw,
            };

            var pending = handler(evt);
            if (pending.IsFaulted || !pending.IsCompleted)
            {
                _ = ObserveAsync(pending);
            }
        }

        private static async Task ObserveAsync(Task pending)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A subscriber failure must not tear down the stream.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (_gate)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }

            if (_ownsHttp)
            {
                _http.Dispose();
            }
        }
    }
}
