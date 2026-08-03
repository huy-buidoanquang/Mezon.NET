using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    /// <summary>
    ///     Enforces Mezon socket transport limits: per-second, per-minute, and connect-phase caps.
    /// </summary>
    internal sealed class TransportRateLimiter
    {
        private SlidingWindowRateLimiter _perSecond;
        private SlidingWindowRateLimiter _perMinute;
        private SlidingWindowRateLimiter _connectPerSecond;
        private int _connectPhase;

        public TransportRateLimiter(
            int maxRequestsPerSecond = MezonTransportLimits.MaxRequestsPerSecond,
            int maxRequestsPerMinute = MezonTransportLimits.MaxRequestsPerMinute,
            int maxConnectRequestsPerSecond = MezonTransportLimits.MaxConnectRequestsPerSecond)
        {
            _perSecond = new SlidingWindowRateLimiter(maxRequestsPerSecond, 1);
            _perMinute = new SlidingWindowRateLimiter(maxRequestsPerMinute, 60);
            _connectPerSecond = new SlidingWindowRateLimiter(maxConnectRequestsPerSecond, 1);
        }

        /// <summary>
        ///     Updates limit capacities in place without replacing this instance.
        /// </summary>
        public void Configure(
            int maxRequestsPerSecond,
            int maxRequestsPerMinute,
            int maxConnectRequestsPerSecond)
        {
            _perSecond = new SlidingWindowRateLimiter(maxRequestsPerSecond, 1);
            _perMinute = new SlidingWindowRateLimiter(maxRequestsPerMinute, 60);
            _connectPerSecond = new SlidingWindowRateLimiter(maxConnectRequestsPerSecond, 1);
            EndConnectPhase();
        }

        public void BeginConnectPhase() => Volatile.Write(ref _connectPhase, 1);

        public void EndConnectPhase() => Volatile.Write(ref _connectPhase, 0);

        public async ValueTask EnterAsync(
            CancellationToken cancellationToken = default,
            Func<IRateLimitInfo, Task>? ratelimitCallback = null,
            Func<long, long, string, Task>? sendBypassMessageAsync = null)
        {
            if (Volatile.Read(ref _connectPhase) != 0)
            {
                await WaitBucketAsync(
                    _connectPerSecond,
                    RateLimitBuckets.TransportConnect,
                    isGlobal: false,
                    cancellationToken,
                    ratelimitCallback,
                    sendBypassMessageAsync).ConfigureAwait(false);
            }

            await WaitBucketAsync(
                _perSecond,
                RateLimitBuckets.TransportPerSecond,
                isGlobal: true,
                cancellationToken,
                ratelimitCallback,
                sendBypassMessageAsync).ConfigureAwait(false);

            await WaitBucketAsync(
                _perMinute,
                RateLimitBuckets.TransportPerMinute,
                isGlobal: true,
                cancellationToken,
                ratelimitCallback,
                sendBypassMessageAsync).ConfigureAwait(false);
        }

        public void Reset()
        {
            _perSecond.Reset();
            _perMinute.Reset();
            _connectPerSecond.Reset();
            EndConnectPhase();
        }

        private static async ValueTask WaitBucketAsync(
            SlidingWindowRateLimiter limiter,
            string bucket,
            bool isGlobal,
            CancellationToken cancellationToken,
            Func<IRateLimitInfo, Task>? ratelimitCallback,
            Func<long, long, string, Task>? sendBypassMessageAsync)
        {
            Action<int>? onDelayed = null;
            if (ratelimitCallback != null)
            {
                onDelayed = delayMs =>
                {
                    var info = new RateLimitInfo
                    {
                        IsGlobal = isGlobal,
                        Limit = limiter.MaxCount,
                        Remaining = 0,
                        ResetAfter = TimeSpan.FromMilliseconds(delayMs),
                        Bucket = bucket,
                        SendBypassMessageAsync = sendBypassMessageAsync
                    };
                    _ = InvokeCallbackAsync(ratelimitCallback, info);
                };
            }

            await limiter.WaitAsync(cancellationToken, onDelayed).ConfigureAwait(false);
        }

        private static async Task InvokeCallbackAsync(Func<IRateLimitInfo, Task> callback, IRateLimitInfo info)
        {
            try
            {
                await callback(info).ConfigureAwait(false);
            }
            catch
            {
                // Callbacks must not break the send path.
            }
        }
    }
}
