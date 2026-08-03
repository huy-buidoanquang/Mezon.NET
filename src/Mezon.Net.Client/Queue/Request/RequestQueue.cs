using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Utils;
using Newtonsoft.Json;

namespace Mezon.Net.Queue
{
    /// <summary>
    ///     Lean request pipeline. Socket traffic is throttled by <see cref="TransportRateLimiter"/>;
    ///     the REST path only carries rare auth-bootstrap calls and is sent directly.
    /// </summary>
    internal sealed class RequestQueue : IDisposable, IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _clearToken = new CancellationTokenSource();
        private CancellationToken _parentToken = CancellationToken.None;
        private CancellationTokenSource? _requestCancelTokenSource;
        private CancellationToken _requestCancelToken = CancellationToken.None;
        private readonly TransportRateLimiter _transportLimiter = new TransportRateLimiter();
        private Func<IRateLimitInfo, Task>? _defaultRatelimitCallback;
        private Func<long, long, string, Task>? _sendBypassMessageAsync;

        /// <summary>
        ///     Updates transport rate-limit capacities without replacing the limiter instance.
        /// </summary>
        internal void ConfigureTransportLimits(
            int maxRequestsPerSecond,
            int maxRequestsPerMinute,
            int maxConnectRequestsPerSecond)
        {
            _transportLimiter.Configure(
                maxRequestsPerSecond,
                maxRequestsPerMinute,
                maxConnectRequestsPerSecond);
        }

        /// <summary>
        ///     Sets the client-wide default rate-limit callback applied when a request has none.
        /// </summary>
        internal void SetDefaultRatelimitCallback(Func<IRateLimitInfo, Task>? callback)
        {
            _defaultRatelimitCallback = callback;
        }

        /// <summary>
        ///     Sets the bypass send used by <see cref="IRateLimitInfo.SendBypassMessageAsync"/> in rate-limit callbacks.
        /// </summary>
        internal void SetRateLimitBypassMessage(Func<long, long, string, Task>? sendBypassMessageAsync)
        {
            _sendBypassMessageAsync = sendBypassMessageAsync;
        }

        internal ValueTask EnterTransportAsync(RequestOptions options)
        {
            options.ApplyDefaultRatelimitCallback(_defaultRatelimitCallback);
            return _transportLimiter.EnterAsync(options.CancelToken, options.RatelimitCallback, _sendBypassMessageAsync);
        }

        internal void BeginConnectPhase() => _transportLimiter.BeginConnectPhase();

        internal void EndConnectPhase() => _transportLimiter.EndConnectPhase();

        internal void ResetTransportLimits() => _transportLimiter.Reset();

        public async Task SetCancelTokenAsync(CancellationToken cancelToken)
        {
            await _semaphoreLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _parentToken = cancelToken;
                _requestCancelTokenSource?.Dispose();
                _requestCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _clearToken.Token);
                _requestCancelToken = _requestCancelTokenSource.Token;
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }

        public async Task ClearAsync()
        {
            await _semaphoreLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _clearToken?.Cancel();
                _clearToken?.Dispose();
                _clearToken = new CancellationTokenSource();
                _requestCancelTokenSource?.Dispose();
                _requestCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_clearToken.Token, _parentToken);
                _requestCancelToken = _requestCancelTokenSource.Token;
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }

        public async Task<Stream> SendAsync(ApiRequest request)
        {
            CancellationTokenSource? createdTokenSource = null;
            var requestToken = request.Options.CancelToken;

            if (requestToken.CanBeCanceled && _requestCancelToken.CanBeCanceled)
            {
                createdTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_requestCancelToken, requestToken);
                request.Options.CancelToken = createdTokenSource.Token;
            }
            else if (requestToken.CanBeCanceled)
            {
                request.Options.CancelToken = requestToken;
            }
            else
            {
                request.Options.CancelToken = _requestCancelToken;
            }

            try
            {
                var response = await request.SendAsync().ConfigureAwait(false);
                if (response.StatusCode < (HttpStatusCode)200 || response.StatusCode >= (HttpStatusCode)300)
                {
                    throw BuildHttpException(request, response);
                }

                return response.Stream!;
            }
            finally
            {
                createdTokenSource?.Dispose();
            }
        }

        private static HttpException BuildHttpException(ApiRequest request, HttpResponse response)
        {
            string? reason = null;

            if (response.Stream != null)
            {
                try
                {
                    if (response.Stream.CanSeek && response.Stream.Length == 0)
                    {
                        return new HttpException(response.StatusCode, request, reason);
                    }

                    using var reader = new StreamReader(response.Stream);
                    var json = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var error = JsonConvert.DeserializeObject<MezonErrorResponse>(json, Json.JsonSerializerSettings);
                        reason = error?.Message ?? json;
                    }
                }
                catch
                {
                    // Best-effort error body parsing.
                }
            }

            return new HttpException(response.StatusCode, request, reason);
        }

        public void Dispose()
        {
            _semaphoreLock.Dispose();
            _clearToken?.Dispose();
            _requestCancelTokenSource?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
