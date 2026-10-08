using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Per-channel send serialization with idle gate pruning.
    /// </summary>
    /// <remarks>
    ///     Each gate counts the callers using it. A gate is retired (removed from the map) only under its own lock
    ///     while nobody uses it, so a caller never waits on a gate that was pruned and two gates never exist for one
    ///     channel. Semaphores are not disposed: without <see cref="SemaphoreSlim.AvailableWaitHandle"/> they hold no
    ///     unmanaged resources.
    /// </remarks>
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
            var gate = AcquireGate(channelId);
            try
            {
                await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await action().ConfigureAwait(false);
                }
                finally
                {
                    gate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseGate(gate);
                MaybePrune();
            }
        }

        public Task EnqueueAsync(long channelId, Func<Task> action, CancellationToken cancellationToken = default)
            => EnqueueAsync(channelId, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, cancellationToken);

        private GateState AcquireGate(long channelId)
        {
            if (_locks.Count >= _maxChannels)
            {
                PruneIdleGates(force: true);
            }

            while (true)
            {
                var gate = _locks.GetOrAdd(channelId, _ => new GateState());
                lock (gate)
                {
                    if (!gate.Retired)
                    {
                        gate.Users++;
                        return gate;
                    }
                }

                // Pruned between the lookup and the lock; it is no longer in the map, so look up again.
            }
        }

        private static void ReleaseGate(GateState gate)
        {
            lock (gate)
            {
                gate.Users--;
                gate.LastUsedUtc = DateTime.UtcNow;
            }
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
                lock (state)
                {
                    if (state.Users != 0 || (!force && state.LastUsedUtc > cutoff))
                    {
                        continue;
                    }

                    if (!((ICollection<KeyValuePair<long, GateState>>)_locks).Remove(pair))
                    {
                        continue;
                    }

                    state.Retired = true;
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

            /// <summary>Callers that acquired this gate and have not released it; guarded by <c>lock(this)</c>.</summary>
            public int Users { get; set; }

            /// <summary>Removed from the map; guarded by <c>lock(this)</c>.</summary>
            public bool Retired { get; set; }
        }
    }
}
