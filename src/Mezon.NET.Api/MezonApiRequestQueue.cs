using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api
{
    internal class MezonApiRequestQueue : IDisposable, IAsyncDisposable
    {
        public event Func<BucketId, RateLimitInfo?, string, Task> RateLimitTriggered;

        private readonly ConcurrentDictionary<BucketId, object> _buckets;
        private readonly SemaphoreSlim _tokenLock;
        private readonly CancellationTokenSource _cancelTokenSource; //Dispose token
        private CancellationTokenSource _clearToken;
        private CancellationToken _parentToken;
        private CancellationTokenSource _requestCancelTokenSource;
        private CancellationToken _requestCancelToken; //Parent token + Clear token
        private DateTimeOffset _waitUntil;

        private Task _cleanupTask;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MezonApiRequestQueue()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _tokenLock = new SemaphoreSlim(1, 1);

            _clearToken = new CancellationTokenSource();
            _cancelTokenSource = new CancellationTokenSource();
            _requestCancelToken = CancellationToken.None;
            _parentToken = CancellationToken.None;

            _buckets = new ConcurrentDictionary<BucketId, object>();

            _cleanupTask = RunCleanup();
        }

        public async Task SetCancelTokenAsync(CancellationToken cancelToken)
        {
            await _tokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _parentToken = cancelToken;
                _requestCancelTokenSource?.Dispose();
                _requestCancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _clearToken.Token);
                _requestCancelToken = _requestCancelTokenSource.Token;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task ClearAsync()
        {
            await _tokenLock.WaitAsync().ConfigureAwait(false);
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
                _tokenLock.Release();
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

        public async Task<TResponse> SendAsync<TRequest, TResponse>(RpcRequest<TRequest, TResponse> request)
            where TResponse : class
            where TRequest : class
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

        //public async Task SendAsync(WebSocketRequest request)
        //{
        //    CancellationTokenSource createdTokenSource = null;
        //    if (request.Options.CancelToken.CanBeCanceled)
        //    {
        //        createdTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_requestCancelToken, request.Options.CancelToken);
        //        request.Options.CancelToken = createdTokenSource.Token;
        //    }
        //    else
        //        request.Options.CancelToken = _requestCancelToken;

        //    var bucket = GetOrCreateBucket(request.Options, request);
        //    await bucket.SendAsync(request).ConfigureAwait(false);
        //    createdTokenSource?.Dispose();
        //}

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


        internal Task EnterGlobalAsync<TRequest, TResponse>(int id, RpcRequest<TRequest, TResponse> request)
            where TResponse : class
            where TRequest : class
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

        //internal Task EnterGlobalAsync(int id, WebSocketRequest request)
        //{
        //    //If this is a global request (unbucketed), it'll be dealt in EnterAsync
        //    var requestBucket = GatewayBucket.Get(request.Options.BucketId);
        //    if (requestBucket.Type == GatewayBucketType.Unbucketed)
        //        return Task.CompletedTask;

        //    //It's not a global request, so need to remove one from global (per-session)
        //    var globalBucketType = GatewayBucket.Get(GatewayBucketType.Unbucketed);
        //    var options = RequestOptions.CreateOrClone(request.Options);
        //    options.BucketId = globalBucketType.Id;
        //    var globalRequest = new WebSocketRequest(null, null, false, false, options);
        //    var globalBucket = GetOrCreateBucket(options, globalRequest);
        //    return globalBucket.TriggerAsync(id, globalRequest);
        //}

        internal void PauseGlobal(RateLimitInfo info)
        {
            if (info.RetryAfter is null)
            {
                return;
            }

            _waitUntil = DateTimeOffset.UtcNow.AddMilliseconds(info.RetryAfter.Value + (info.Lag?.TotalMilliseconds ?? 0.0));
        }

        private RequestBucket GetOrCreateBucket(RequestOptions options, IRequest request)
        {
            if (options.BucketId == null)
            {
                throw new InvalidOperationException("BucketId is null when trying to get or create a bucket");
            }

            var bucketId = options.BucketId;
            object obj = _buckets.GetOrAdd(bucketId, x => new RequestBucket(this, request, x));
            if (obj is BucketId hashBucket)
            {
                options.BucketId = hashBucket;
                return (RequestBucket)_buckets.GetOrAdd(hashBucket, x => new RequestBucket(this, request, x));
            }
            return (RequestBucket)obj;
        }
        internal Task RaiseRateLimitTriggered(BucketId bucketId, RateLimitInfo? info, string endpoint)
            => RateLimitTriggered(bucketId, info, endpoint);

        internal (RequestBucket?, BucketId?) UpdateBucketHash(BucketId id, string discordHash)
        {
            if (!id.IsHashBucket)
            {
                var bucket = BucketId.Create(discordHash, id);
                var hashReqQueue = (RequestBucket)_buckets.GetOrAdd(bucket, _buckets[id]);
                _buckets.AddOrUpdate(id, bucket, (oldBucket, oldObj) => bucket);
                return (hashReqQueue, bucket);
            }
            return (null, null);
        }

        public void ClearGatewayBuckets()
        {
            foreach (var gwBucket in (GatewayBucketType[])Enum.GetValues(typeof(GatewayBucketType)))
            {
                _buckets.TryRemove(GatewayBucket.Get(gwBucket).Id, out _);
            }
        }

        private async Task RunCleanup()
        {
            try
            {
                while (!_cancelTokenSource.IsCancellationRequested)
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var bucket in _buckets.Where(x => x.Value is RequestBucket).Select(x => (RequestBucket)x.Value))
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
            _tokenLock?.Dispose();
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
            _tokenLock?.Dispose();
            _clearToken?.Dispose();
            _requestCancelTokenSource?.Dispose();
        }
    }
}
