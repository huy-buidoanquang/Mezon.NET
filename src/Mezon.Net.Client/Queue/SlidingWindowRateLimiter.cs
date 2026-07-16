using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Queue
{
    /// <summary>
    ///     Fixed-size sliding window limiter. Fast path returns a completed <see cref="ValueTask"/> with no allocation.
    /// </summary>
    internal sealed class SlidingWindowRateLimiter
    {
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        private readonly long[] _slots;
        private readonly int _capacity;
        private readonly long _windowMs;
        private int _oldest;
        private int _count;
        private readonly object _gate = new object();

        public SlidingWindowRateLimiter(int maxCount, int windowSeconds)
        {
            if (maxCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            if (windowSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            }

            _capacity = maxCount;
            _windowMs = windowSeconds * 1000L;
            _slots = new long[maxCount];
        }

        /// <summary>
        ///     Gets the maximum number of acquisitions allowed within the window.
        /// </summary>
        public int MaxCount => _capacity;

        /// <summary>
        ///     Gets the sliding window length in seconds.
        /// </summary>
        public int WindowSeconds => (int)(_windowMs / 1000L);

        /// <summary>
        ///     Gets the number of acquisitions currently counted in the window.
        /// </summary>
        public int CurrentCount
        {
            get
            {
                lock (_gate)
                {
                    PruneExpired(MonotonicMs());
                    return _count;
                }
            }
        }

        private static long MonotonicMs() => (long)(Stopwatch.GetTimestamp() * MsPerTick);

        /// <summary>
        ///     Waits until a slot is available, then acquires it.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the wait.</param>
        /// <param name="onDelayed">
        ///     Optional callback invoked once per wait cycle when the caller must delay before acquiring.
        ///     Receives the delay in milliseconds.
        /// </param>
        public ValueTask WaitAsync(
            CancellationToken cancellationToken = default,
            Action<int>? onDelayed = null)
        {
            if (TryAcquireOrGetDelay(out var delayMs))
            {
                return default;
            }

            var delay = delayMs > int.MaxValue ? int.MaxValue : (int)delayMs;
            return new ValueTask(WaitCoreAsync(delay, cancellationToken, onDelayed));
        }

        public bool TryAcquire()
        {
            lock (_gate)
            {
                var now = MonotonicMs();
                PruneExpired(now);

                if (_count >= _capacity)
                {
                    return false;
                }

                _slots[(_oldest + _count) % _capacity] = now;
                _count++;
                return true;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _count = 0;
                _oldest = 0;
            }
        }

        private bool TryAcquireOrGetDelay(out long delayMs)
        {
            lock (_gate)
            {
                var now = MonotonicMs();
                PruneExpired(now);

                if (_count < _capacity)
                {
                    _slots[(_oldest + _count) % _capacity] = now;
                    _count++;
                    delayMs = 0;
                    return true;
                }

                var oldestTime = _slots[_oldest];
                delayMs = oldestTime + _windowMs - now;
                if (delayMs <= 0)
                {
                    _slots[_oldest] = now;
                    _oldest = (_oldest + 1) % _capacity;
                    delayMs = 0;
                    return true;
                }

                return false;
            }
        }

        private void PruneExpired(long now)
        {
            while (_count > 0)
            {
                if (now - _slots[_oldest] < _windowMs)
                {
                    break;
                }

                _oldest = (_oldest + 1) % _capacity;
                _count--;
            }
        }

        private async Task WaitCoreAsync(int initialDelayMs, CancellationToken cancellationToken, Action<int>? onDelayed)
        {
            var delayMs = initialDelayMs;
            while (true)
            {
                if (delayMs > int.MaxValue)
                {
                    delayMs = int.MaxValue;
                }

                onDelayed?.Invoke(delayMs);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

                if (TryAcquireOrGetDelay(out var nextDelay))
                {
                    return;
                }

                delayMs = (int)Math.Min(nextDelay, int.MaxValue);
            }
        }
    }
}
