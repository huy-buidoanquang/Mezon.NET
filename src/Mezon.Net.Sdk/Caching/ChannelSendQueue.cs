using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Per-channel send serialization with idle gate pruning.
    /// </summary>
    public sealed class ChannelSendQueue
    {
        private readonly ConcurrentDictionary<long, GateState> _locks = new ConcurrentDictionary<long, GateState>();
        private readonly int _maxChannels;
        private readonly TimeSpan _idleLifetime;

        public ChannelSendQueue(int maxChannels = 10_000, TimeSpan? idleLifetime = null)
        {
            _maxChannels = maxChannels < 16 ? 16 : maxChannels;
            _idleLifetime = idleLifetime ?? TimeSpan.FromMinutes(10);
        }

        public async Task<T> EnqueueAsync<T>(long channelId, Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            var gate = GetOrAddGate(channelId);
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                gate.LastUsedUtc = DateTime.UtcNow;
                return await action().ConfigureAwait(false);
            }
            finally
            {
                gate.Semaphore.Release();
                MaybePrune();
            }
        }

        public Task EnqueueAsync(long channelId, Func<Task> action, CancellationToken cancellationToken = default)
            => EnqueueAsync(channelId, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, cancellationToken);

        private GateState GetOrAddGate(long channelId)
        {
            if (_locks.Count >= _maxChannels)
            {
                PruneIdleGates(force: true);
            }

            return _locks.GetOrAdd(channelId, _ => new GateState());
        }

        private void MaybePrune()
        {
            if (_locks.Count < _maxChannels / 2)
            {
                return;
            }

            PruneIdleGates(force: false);
        }

        private void PruneIdleGates(bool force)
        {
            var cutoff = DateTime.UtcNow - _idleLifetime;
            foreach (var pair in _locks)
            {
                var state = pair.Value;
                if (!force && state.LastUsedUtc > cutoff)
                {
                    continue;
                }

                if (state.Semaphore.CurrentCount != 1)
                {
                    continue;
                }

                if (_locks.TryRemove(pair.Key, out var removed))
                {
                    removed.Semaphore.Dispose();
                }

                if (!force && _locks.Count < _maxChannels / 2)
                {
                    break;
                }
            }
        }

        private sealed class GateState
        {
            public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
            public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
        }
    }
}
