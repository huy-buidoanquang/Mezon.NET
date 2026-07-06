using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Agent
{
    public sealed class AgentSseSessionEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string RawData { get; set; } = string.Empty;
    }

    public sealed class AgentSseManager : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _endpoint;
        private readonly string _appId;
        private readonly string _token;
        private CancellationTokenSource? _cts;

        public event Func<AgentSseSessionEvent, Task>? MessageReceived;

        public AgentSseManager(string baseUrl, string appId, string token, HttpClient? httpClient = null)
        {
            _endpoint = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "api/sse/metadata");
            _appId = appId;
            _token = token;
            _http = httpClient ?? new HttpClient();
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return Task.Run(() => ReadLoopAsync(_cts.Token), CancellationToken.None);
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _token);
            request.Headers.TryAddWithoutValidation("X-App-Id", _appId);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var dataBuilder = new StringBuilder();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    dataBuilder.Append(line.AsSpan(5).Trim());
                    continue;
                }

                if (line.Length == 0 && dataBuilder.Length > 0)
                {
                    var raw = dataBuilder.ToString();
                    dataBuilder.Clear();
                    await DispatchAsync(raw).ConfigureAwait(false);
                }
            }
        }

        private async Task DispatchAsync(string raw)
        {
            if (MessageReceived == null)
            {
                return;
            }

            using var doc = JsonDocument.Parse(raw);
            var eventType = doc.RootElement.TryGetProperty("event_type", out var typeElement)
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

            await MessageReceived.Invoke(new AgentSseSessionEvent
            {
                EventType = eventType,
                RawData = raw,
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _http.Dispose();
        }
    }
}
