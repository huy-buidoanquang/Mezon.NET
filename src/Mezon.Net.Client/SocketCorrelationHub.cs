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
        private readonly SocketCorrelationHub _owner;
        private readonly int _cid;
        private ManualResetValueTaskSourceCore<SocketResponse> _core;
        private short _version;
        private CancellationTokenRegistration _cancellationRegistration;

        public PendingSocketRequest(SocketCorrelationHub owner, int cid)
        {
            _owner = owner;
            _cid = cid;
            _core = new ManualResetValueTaskSourceCore<SocketResponse>
            {
                RunContinuationsAsynchronously = true
            };
        }

        public ValueTask<SocketResponse> Task => new(this, _version);

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

        public void StartTimeout(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds <= 0 || timeoutMilliseconds == Timeout.Infinite)
            {
                return;
            }

            _ = System.Threading.Tasks.Task.Delay(timeoutMilliseconds).ContinueWith(static (t, state) =>
            {
                if (t.IsCanceled)
                {
                    return;
                }

                var pending = (PendingSocketRequest)state!;
                pending.Abort(new TimeoutException("The socket timed out while waiting for a response."));
            }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Abort(Exception error) => _owner.TryFail(_cid, this, error);

        public void TrySetResult(SocketResponse result)
        {
            _cancellationRegistration.Dispose();
            _core.SetResult(result);
        }

        public void TrySetException(Exception error)
        {
            _cancellationRegistration.Dispose();
            _core.SetException(error);
        }

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

        public ValueTask<SocketResponse> Task => _pending.Task;

        public void StartTimeout(int timeoutMilliseconds) => _pending.StartTimeout(timeoutMilliseconds);

        public void Abort(Exception error) => _pending.Abort(error);
    }

    /// <summary>
    /// Correlates outbound socket requests with inbound responses by correlation id (cid).
    /// </summary>
    internal sealed class SocketCorrelationHub
    {
        private readonly ConcurrentDictionary<int, PendingSocketRequest> _pending = new();
        private int _nextCid = 1;
        public const int DefaultTimeoutMilliseconds = 10_000;

        public int AllocateCid()
        {
            while (true)
            {
                var cid = Interlocked.Increment(ref _nextCid);
                if (cid > ushort.MaxValue)
                {
                    Interlocked.Exchange(ref _nextCid, 1);
                    cid = 1;
                }

                if (!_pending.ContainsKey(cid))
                {
                    return cid;
                }
            }
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
