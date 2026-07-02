using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    internal class RequestQueue : IDisposable, IAsyncDisposable
    {
        public event Func<BucketId, RateLimitInfo?, string, Task> RateLimitTriggered;

        private readonly ConcurrentDictionary<BucketId, object> _buckets;
        private readonly SemaphoreSlim _semaphoreLock;
        private readonly CancellationTokenSource _cancelTokenSource;
        private CancellationTokenSource _clearToken;
        private CancellationToken _parentToken;
        private CancellationTokenSource _requestCancelTokenSource;
        private CancellationToken _requestCancelToken;
        private DateTimeOffset _waitUntil;

        // Gateway rate limiters (WebSocket only)
        private readonly SocketRateLimiter _unbucketedLimiter;
        private readonly SocketRateLimiter _identifyLimiter;
        private readonly SocketRateLimiter _presenceUpdateLimiter;

        private Task _cleanupTask;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public RequestQueue()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _semaphoreLock = new SemaphoreSlim(1, 1);

            _clearToken = new CancellationTokenSource();
            _cancelTokenSource = new CancellationTokenSource();
            _requestCancelToken = CancellationToken.None;
            _parentToken = CancellationToken.None;
            _buckets = new ConcurrentDictionary<BucketId, object>();

            _unbucketedLimiter = new SocketRateLimiter(SocketBucketType.Unbucketed, 117, 60);
            _identifyLimiter = new SocketRateLimiter(SocketBucketType.Identify, 1, 5);
            _presenceUpdateLimiter = new SocketRateLimiter(SocketBucketType.PresenceUpdate, 5, 60);

            _cleanupTask = RunCleanup();
        }

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
            CancellationTokenSource? createdTokenSource = default;
            if (request.Options.CancelToken.CanBeCanceled)
            {
                createdTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_requestCancelToken, request.Options.CancelToken);
                request.Options.CancelToken = createdTokenSource.Token;
            }
            else
            {
                request.Options.CancelToken = _requestCancelToken;
            }

            var bucket = GetOrCreateBucket(request.Options, request);
            var result = await bucket.SendAsync(request).ConfigureAwait(false);
            createdTokenSource?.Dispose();
            return result;
        }

        internal Task EnterGlobalAsync(int id, ApiRequest request)
        {
            int millis = (int)Math.Ceiling((_waitUntil - DateTimeOffset.UtcNow).TotalMilliseconds);
            if (millis > 0)
            {
#if DEBUG_LIMITS
                Debug.WriteLine($"[{id}] Sleeping {millis} ms (Pre-emptive) [Global]");
#endif
                return Task.Delay(millis);
            }

            return Task.CompletedTask;
        }

        public async Task SendAsync(WebSocketRequest request)
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

            var bucketType = request.Options.BucketId != null
                ? SocketBucket.Get(request.Options.BucketId).Type
                : SocketBucketType.Unbucketed;
            await EnterGatewayAsync(request.Options, bucketType).ConfigureAwait(false);
            await request.SendAsync().ConfigureAwait(false);

            createdTokenSource?.Dispose();
        }

        internal async Task EnterGatewayAsync(RequestOptions options, SocketBucketType bucketType = SocketBucketType.Unbucketed)
        {
            options.BucketId ??= SocketBucket.Get(bucketType).Id;
            await EnterGatewayLimiterAsync(options).ConfigureAwait(false);
        }

        internal Task EnterGlobalAsync(int id, WebSocketRequest request)
            => EnterGatewayLimiterAsync(request.Options);

        private async Task EnterGatewayLimiterAsync(RequestOptions options)
        {
            int millis = (int)Math.Ceiling((_waitUntil - DateTimeOffset.UtcNow).TotalMilliseconds);
            if (millis > 0)
            {
#if DEBUG_LIMITS
                System.Diagnostics.Debug.WriteLine($"[Gateway] Sleeping {millis} ms (Pre-emptive) [Global]");
#endif
                await Task.Delay(millis, options.CancelToken).ConfigureAwait(false);
            }

            var requestBucket = SocketBucket.Get(options.BucketId!);
            if (requestBucket.Type != SocketBucketType.Unbucketed)
            {
                await _unbucketedLimiter.WaitAsync(options.CancelToken).ConfigureAwait(false);
            }

            var limiter = requestBucket.Type switch
            {
                SocketBucketType.Unbucketed => _unbucketedLimiter,
                SocketBucketType.Identify => _identifyLimiter,
                SocketBucketType.PresenceUpdate => _presenceUpdateLimiter,
                _ => _unbucketedLimiter
            };

            await limiter.WaitAsync(options.CancelToken).ConfigureAwait(false);
        }

        internal void PauseGlobal(RateLimitInfo info)
        {
            if (info.RetryAfter is null)
            {
                return;
            }

            _waitUntil = DateTimeOffset.UtcNow.AddMilliseconds(info.RetryAfter.Value + (info.Lag?.TotalMilliseconds ?? 0.0));
        }

        private RequestQueueBucket GetOrCreateBucket(RequestOptions options, IRequest request)
        {
            if (options.BucketId == null)
            {
                throw new InvalidOperationException("BucketId is null when trying to get or create a bucket");
            }

            var bucketId = options.BucketId;
            object obj = _buckets.GetOrAdd(bucketId, x => new RequestQueueBucket(this, request, x));
            if (obj is BucketId hashBucket)
            {
                options.BucketId = hashBucket;
                return (RequestQueueBucket)_buckets.GetOrAdd(hashBucket, x => new RequestQueueBucket(this, request, x));
            }
            return (RequestQueueBucket)obj;
        }

        internal Task RaiseRateLimitTriggered(BucketId bucketId, RateLimitInfo? info, string endpoint)
            => RateLimitTriggered(bucketId, info, endpoint);

        internal (RequestQueueBucket?, BucketId?) UpdateBucketHash(BucketId id, string mezonHash)
        {
            if (!id.IsHashBucket && !string.IsNullOrWhiteSpace(mezonHash))
            {
                var bucket = BucketId.Create(mezonHash, id);
                var hashReqQueue = (RequestQueueBucket)_buckets.GetOrAdd(bucket, _buckets[id]);
                _buckets.AddOrUpdate(id, bucket, (oldBucket, oldObj) => bucket);
                return (hashReqQueue, bucket);
            }
            return (null, null);
        }

        public void ClearGatewayBuckets()
        {
            // Reset gateway rate limiters
            _unbucketedLimiter.Reset();
            _identifyLimiter.Reset();
            _presenceUpdateLimiter.Reset();
        }

        private async Task RunCleanup()
        {
            try
            {
                while (!_cancelTokenSource.IsCancellationRequested)
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var bucket in _buckets.Where(x => x.Value is RequestQueueBucket).Select(x => (RequestQueueBucket)x.Value))
                    {
                        if ((now - bucket.LastAttemptAt).TotalMinutes > 1.0)
                        {
                            if (bucket.Id.IsHashBucket)
                            {
                                foreach (var redirectBucket in _buckets.Where(x => x.Value == bucket.Id).Select(x => (BucketId)x.Value))
                                {
                                    _buckets.TryRemove(redirectBucket, out _); //remove redirections if hash bucket
                                }
                            }

                            _buckets.TryRemove(bucket.Id, out _);
                        }
                    }
                    await Task.Delay(60000, _cancelTokenSource.Token).ConfigureAwait(false); //Runs each minute
                }
            }
            catch (TaskCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            if (!(_cancelTokenSource is null))
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                _cleanupTask.GetAwaiter().GetResult();
            }
            _semaphoreLock?.Dispose();
            _clearToken?.Dispose();
            _requestCancelTokenSource?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (!(_cancelTokenSource is null))
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                await _cleanupTask.ConfigureAwait(false);
            }
            _semaphoreLock?.Dispose();
            _clearToken?.Dispose();
            _requestCancelTokenSource?.Dispose();
        }
    }
}
