using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Core;

namespace Mezon.NET.Api
{
    /// <summary>
    ///     Simple rate limiter for WebSocket gateway operations.
    /// </summary>
    internal class GatewayRateLimiter
    {
        private readonly int _maxCount;
        private readonly int _windowSeconds;
        private readonly Queue<DateTimeOffset> _timestamps;
        private readonly SemaphoreSlim _lock;

        public BucketType Type { get; }
        public int WindowCount => _maxCount;
        public int WindowSeconds => _windowSeconds;

        public GatewayRateLimiter(BucketType type, int maxCount, int windowSeconds)
        {
            Type = type;
            _maxCount = maxCount;
            _windowSeconds = windowSeconds;
            _timestamps = new Queue<DateTimeOffset>(maxCount);
            _lock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        ///     Wait until rate limit allows sending. Returns immediately if under limit.
        /// </summary>
        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var window = TimeSpan.FromSeconds(_windowSeconds);

                // Remove expired timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < now - window)
                {
                    _timestamps.Dequeue();
                }

                // If at limit, wait until oldest timestamp expires
                if (_timestamps.Count >= _maxCount)
                {
                    var oldestTimestamp = _timestamps.Peek();
                    var delay = (oldestTimestamp + window) - now;
                    
                    if (delay > TimeSpan.Zero)
                    {
#if DEBUG_LIMITS
                        System.Diagnostics.Debug.WriteLine($"[Gateway {Type}] Rate limited, waiting {delay.TotalMilliseconds:F0}ms");
#endif
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }

                    // After waiting, remove expired timestamp
                    _timestamps.Dequeue();
                }

                // Add current timestamp
                _timestamps.Enqueue(DateTimeOffset.UtcNow);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        ///     Try to acquire without waiting. Returns false if rate limited.
        /// </summary>
        public bool TryAcquire()
        {
            _lock.Wait();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var window = TimeSpan.FromSeconds(_windowSeconds);

                // Remove expired timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < now - window)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= _maxCount)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        ///     Reset the rate limiter state.
        /// </summary>
        public void Reset()
        {
            _lock.Wait();
            try
            {
                _timestamps.Clear();
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
