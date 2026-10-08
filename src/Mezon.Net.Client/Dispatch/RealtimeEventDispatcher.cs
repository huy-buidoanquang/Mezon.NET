using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Logging;

namespace Mezon.Net.Client.Dispatch
{
    /// <summary>
    /// Delivers realtime events in order per key (channel, else clan) on a fixed set of lanes, each a bounded queue with
    /// one worker.
    /// </summary>
    /// <remarks>
    /// <para>The socket receive loop only calls <see cref="Enqueue"/>, which never blocks: a handler awaiting a socket
    /// API needs that loop to read the response. When a lane is full the new event is dropped, counted and reported.</para>
    /// <para>A handler that runs longer than the advance timeout keeps running, but its lane moves on, so a slow or
    /// stuck handler (or one waiting for a later event on its own lane) cannot stall the lane forever.</para>
    /// </remarks>
    internal sealed class RealtimeEventDispatcher : IAsyncDisposable
    {
        /// <summary>Lane advance timeout used when handler timeouts are disabled.</summary>
        internal const int DefaultAdvanceAfterMilliseconds = 30_000;
        private const int DropWarningIntervalSeconds = 10;

        private readonly Channel<Func<Task>>[] _lanes;
        private readonly Task[] _workers;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Logger _logger;
        private readonly int _advanceAfterMilliseconds;
        private long _droppedCount;
        private long _lastDropWarning;
        private int _disposed;

        public RealtimeEventDispatcher(int laneCount, int laneCapacity, int? advanceAfterMilliseconds, Logger logger)
        {
            if (laneCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "At least one dispatch lane is required.");
            }

            if (laneCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCapacity), laneCapacity, "Lane capacity must be positive.");
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _advanceAfterMilliseconds = advanceAfterMilliseconds is > 0 ? advanceAfterMilliseconds.Value : DefaultAdvanceAfterMilliseconds;
            _lanes = new Channel<Func<Task>>[laneCount];
            _workers = new Task[laneCount];
            for (var i = 0; i < laneCount; i++)
            {
                var lane = Channel.CreateBounded<Func<Task>>(new BoundedChannelOptions(laneCapacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                });
                _lanes[i] = lane;
                _workers[i] = Task.Run(() => RunLaneAsync(lane.Reader));
            }
        }

        /// <summary>Events dropped because their lane was full.</summary>
        public long DroppedCount => Interlocked.Read(ref _droppedCount);

        public void Enqueue(long key, Func<Task> work)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (_lanes[LaneIndex(key)].Writer.TryWrite(work))
            {
                return;
            }

            ReportDrop(Interlocked.Increment(ref _droppedCount));
        }

        internal int LaneIndex(long key)
        {
            if (key == 0 || _lanes.Length == 1)
            {
                return 0;
            }

            // Fibonacci hashing spreads sequential snowflake ids across lanes.
            return (int)((unchecked((ulong)key * 0x9E3779B97F4A7C15UL) >> 32) % (uint)_lanes.Length);
        }

        private async Task RunLaneAsync(ChannelReader<Func<Task>> reader)
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var work))
                {
                    if (_shutdown.IsCancellationRequested)
                    {
                        continue;
                    }

                    await RunOneAsync(work).ConfigureAwait(false);
                }
            }
        }

        private async Task RunOneAsync(Func<Task> work)
        {
            Task handlers;
            try
            {
                handlers = work();
            }
            catch (Exception ex)
            {
                await LogFailureAsync(ex).ConfigureAwait(false);
                return;
            }

            if (handlers.IsCompleted)
            {
                // Fast path: no timer for handlers that finish synchronously.
                await ObserveAsync(handlers).ConfigureAwait(false);
                return;
            }

            using (var advance = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token))
            {
                var completed = await Task.WhenAny(handlers, Task.Delay(_advanceAfterMilliseconds, advance.Token)).ConfigureAwait(false);
                if (completed == handlers)
                {
                    advance.Cancel();
                    await ObserveAsync(handlers).ConfigureAwait(false);
                    return;
                }
            }

            // The handler keeps running; the lane moves on without it.
            _ = ObserveAsync(handlers);
        }

        private async Task ObserveAsync(Task handlers)
        {
            try
            {
                await handlers.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await LogFailureAsync(ex).ConfigureAwait(false);
            }
        }

        private async Task LogFailureAsync(Exception ex)
        {
            try
            {
                await _logger.WarningAsync("Realtime event dispatch failed.", ex).ConfigureAwait(false);
            }
            catch
            {
                // A failing log sink must not stop the lane.
            }
        }

        private void ReportDrop(long dropped)
        {
            var now = Stopwatch.GetTimestamp();
            var last = Interlocked.Read(ref _lastDropWarning);
            if (dropped > 1
                && (now - last < Stopwatch.Frequency * DropWarningIntervalSeconds
                    || Interlocked.CompareExchange(ref _lastDropWarning, now, last) != last))
            {
                return;
            }

            Interlocked.Exchange(ref _lastDropWarning, now);
            _ = _logger.WarningAsync(
                $"Realtime event lane is full; dropped {dropped} event(s) so far. Handlers are too slow for the event rate: keep them short or move work to a queue.");
        }

        /// <summary>Stops accepting events and makes workers skip queued ones; does not wait for them.</summary>
        public void Cancel()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _shutdown.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            foreach (var lane in _lanes)
            {
                lane.Writer.TryComplete();
            }
        }

        public async ValueTask DisposeAsync()
        {
            Cancel();
            try
            {
                // Workers exit promptly: queued events are skipped and in-flight handlers are detached on shutdown.
                await Task.WhenAll(_workers).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
