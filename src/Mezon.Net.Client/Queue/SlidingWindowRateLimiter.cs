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

        public int MaxCount => _capacity;
        public int WindowSeconds => (int)(_windowMs / 1000L);

        private static long MonotonicMs() => Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;

        public ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                long delayMs;
                lock (_gate)
                {
                    var now = MonotonicMs();
                    PruneExpired(now);

                    if (_count < _capacity)
                    {
                        _slots[(_oldest + _count) % _capacity] = now;
                        _count++;
                        return default;
                    }

                    var oldestTime = _slots[_oldest];
                    delayMs = oldestTime + _windowMs - now;
                    if (delayMs <= 0)
                    {
                        _slots[_oldest] = now;
                        _oldest = (_oldest + 1) % _capacity;
                        return default;
                    }
                }

                if (delayMs > int.MaxValue)
                {
                    delayMs = int.MaxValue;
                }

                return new ValueTask(WaitCoreAsync((int)delayMs, cancellationToken));
            }
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

        private async Task WaitCoreAsync(int delayMs, CancellationToken cancellationToken)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }
    }
}
