using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Mezon.Net.Client
{
    internal readonly struct SocketResponse
    {
        public int Code { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public SocketResponse(int code, ReadOnlyMemory<byte> payload)
        {
            Code = code;
            Payload = payload;
        }
    }

    internal sealed class PendingSocketRequest : IValueTaskSource<SocketResponse>
    {
        private static readonly TimerCallback OnTimeout = static state =>
            ((PendingSocketRequest)state!).Abort(new TimeoutException("The socket timed out while waiting for a response."));

        private readonly SocketCorrelationHub _owner;
        private readonly int _cid;
        private ManualResetValueTaskSourceCore<SocketResponse> _core;
        private short _version;
        private CancellationTokenRegistration _cancellationRegistration;
        private Timer? _timeoutTimer;
        private int _completed;

        public PendingSocketRequest(SocketCorrelationHub owner, int cid)
        {
            _owner = owner;
            _cid = cid;
            _core = new ManualResetValueTaskSourceCore<SocketResponse>
            {
                RunContinuationsAsynchronously = true
            };
        }

        public int Cid => _cid;

        public ValueTask<SocketResponse> Task => new(this, _version);

        internal bool HasTimeoutTimer => Volatile.Read(ref _timeoutTimer) != null;

        public void Initialize(CancellationToken cancellationToken)
        {
            _cancellationRegistration.Dispose();
            _core.Reset();
            _version++;
            if (cancellationToken.CanBeCanceled)
            {
                _cancellationRegistration = cancellationToken.Register(static state =>
                {
                    var pending = (PendingSocketRequest)state!;
                    pending.Abort(new OperationCanceledException());
                }, this);
            }
        }

        /// <summary>
        /// Starts the response timeout. The timer is disposed as soon as the request completes, so a completed request
        /// (and its response payload) is not kept alive until the timeout would have fired.
        /// </summary>
        public void StartTimeout(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds <= 0 || timeoutMilliseconds == Timeout.Infinite || Volatile.Read(ref _completed) != 0)
            {
                return;
            }

            var timer = new Timer(OnTimeout, this, Timeout.Infinite, Timeout.Infinite);
            if (Interlocked.CompareExchange(ref _timeoutTimer, timer, null) != null)
            {
                timer.Dispose();
                return;
            }

            // Completion may have raced the publication above; whoever observes the timer last releases it.
            if (Volatile.Read(ref _completed) != 0)
            {
                ReleaseTimeoutTimer();
                return;
            }

            try
            {
                timer.Change(timeoutMilliseconds, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Abort(Exception error) => _owner.TryFail(_cid, this, error);

        public void TrySetResult(SocketResponse result)
        {
            MarkCompleted();
            _core.SetResult(result);
        }

        public void TrySetException(Exception error)
        {
            MarkCompleted();
            _core.SetException(error);
        }

        private void MarkCompleted()
        {
            Volatile.Write(ref _completed, 1);
            ReleaseTimeoutTimer();
            _cancellationRegistration.Dispose();
        }

        private void ReleaseTimeoutTimer() => Interlocked.Exchange(ref _timeoutTimer, null)?.Dispose();

        SocketResponse IValueTaskSource<SocketResponse>.GetResult(short token) => _core.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource<SocketResponse>.GetStatus(short token) => _core.GetStatus(token);
        void IValueTaskSource<SocketResponse>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
    }

    internal readonly struct PendingSocketRequestHandle
    {
        private readonly PendingSocketRequest _pending;

        public PendingSocketRequestHandle(PendingSocketRequest pending)
        {
            _pending = pending;
        }

        public int Cid => _pending.Cid;

        public ValueTask<SocketResponse> Task => _pending.Task;

        internal bool HasTimeoutTimer => _pending.HasTimeoutTimer;

        public void StartTimeout(int timeoutMilliseconds) => _pending.StartTimeout(timeoutMilliseconds);

        public void Abort(Exception error) => _pending.Abort(error);
    }

    /// <summary>
    /// Correlates outbound socket requests with inbound responses by correlation id (cid).
    /// </summary>
    internal sealed class SocketCorrelationHub
    {
        private const int MaxCid = ushort.MaxValue;
        private readonly ConcurrentDictionary<int, PendingSocketRequest> _pending = new();
        private int _cidCounter;
        public const int DefaultTimeoutMilliseconds = 10_000;

        public SocketCorrelationHub()
        {
        }

        /// <summary>Test hook: starts the cid sequence just before <paramref name="counter"/> + 1.</summary>
        internal SocketCorrelationHub(int counter)
        {
            _cidCounter = counter;
        }

        /// <summary>
        /// Next cid in 1..65535. Derived from one atomic increment, so concurrent callers never share a value while
        /// crossing the wrap point.
        /// </summary>
        private int NextCid()
        {
            var n = unchecked((uint)Interlocked.Increment(ref _cidCounter));
            return (int)((n - 1) % MaxCid) + 1;
        }

        /// <summary>Cid for a fire-and-forget send; skips cids that are waiting for a response.</summary>
        public int AllocateCid()
        {
            for (var attempt = 0; attempt < MaxCid; attempt++)
            {
                var cid = NextCid();
                if (!_pending.ContainsKey(cid))
                {
                    return cid;
                }
            }

            throw new InvalidOperationException($"No free socket correlation id ({MaxCid} requests pending).");
        }

        /// <summary>Reserves a free cid and registers its pending request in one step.</summary>
        public PendingSocketRequestHandle RegisterNext(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < MaxCid; attempt++)
            {
                var pending = new PendingSocketRequest(this, NextCid());
                if (_pending.TryAdd(pending.Cid, pending))
                {
                    pending.Initialize(cancellationToken);
                    return new PendingSocketRequestHandle(pending);
                }
            }

            throw new InvalidOperationException($"No free socket correlation id ({MaxCid} requests pending).");
        }

        public bool Contains(int cid) => _pending.ContainsKey(cid);

        public int PendingCount => _pending.Count;

        public PendingSocketRequestHandle Register(int cid, CancellationToken cancellationToken = default)
        {
            var pending = new PendingSocketRequest(this, cid);
            if (!_pending.TryAdd(cid, pending))
            {
                throw new InvalidOperationException($"Duplicate pending cid {cid}.");
            }

            pending.Initialize(cancellationToken);
            return new PendingSocketRequestHandle(pending);
        }

        public bool TryComplete(int cid, int code, ReadOnlyMemory<byte> payload)
        {
            if (_pending.TryRemove(cid, out var pending))
            {
                pending.TrySetResult(new SocketResponse(code, payload));
                return true;
            }

            return false;
        }

        public bool TryFail(int cid, PendingSocketRequest pending, Exception error)
        {
            if (TryRemoveExact(cid, pending))
            {
                pending.TrySetException(error);
                return true;
            }

            return false;
        }

        public void FailAll(Exception error)
        {
            foreach (var key in _pending.Keys)
            {
                if (_pending.TryRemove(key, out var pending))
                {
                    pending.TrySetException(error);
                }
            }
        }

        private bool TryRemoveExact(int cid, PendingSocketRequest pending)
            => ((ICollection<KeyValuePair<int, PendingSocketRequest>>)_pending)
                .Remove(new KeyValuePair<int, PendingSocketRequest>(cid, pending));
    }
}
