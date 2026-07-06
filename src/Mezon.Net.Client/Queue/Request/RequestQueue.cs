using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
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
        private TransportRateLimiter _transportLimiter = new TransportRateLimiter();

        internal void ConfigureTransportLimits(
            int maxRequestsPerSecond,
            int maxRequestsPerMinute,
            int maxConnectRequestsPerSecond)
        {
            _transportLimiter = new TransportRateLimiter(
                maxRequestsPerSecond,
                maxRequestsPerMinute,
                maxConnectRequestsPerSecond);
        }

        internal ValueTask EnterTransportAsync(RequestOptions options)
            => _transportLimiter.EnterAsync(options.CancelToken);

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
            if (request.Options.CancelToken.CanBeCanceled)
            {
                createdTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_requestCancelToken, request.Options.CancelToken);
                request.Options.CancelToken = createdTokenSource.Token;
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
            MezonErrorResponse? error = null;
            if (response.Stream != null)
            {
                try
                {
                    using var reader = new StreamReader(response.Stream);
                    using var jsonReader = new JsonTextReader(reader);
                    error = Json.Serializer.Deserialize<MezonErrorResponse>(jsonReader);
                }
                catch { }
            }

            MezonJsonError[]? jsonErrors = null;
            if (error?.Errors.IsSpecified == true)
            {
                jsonErrors = error.Errors.Value.Select(x => new MezonJsonError(
                    x.Name.GetValueOrDefault("root"),
                    (x.Errors.GetValueOrDefault(Array.Empty<Error>()) ?? Array.Empty<Error>())
                        .Select(y => new MezonError(y.Code!, y.Message!)).ToArray())).ToArray();
            }

            return new HttpException(
                response.StatusCode,
                request,
                error?.Code ?? MezonErrorCode.GeneralError,
                error?.Message,
                jsonErrors);
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
