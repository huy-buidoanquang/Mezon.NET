using System;
using System.Collections.Concurrent;
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
        private ManualResetValueTaskSourceCore<SocketResponse> _core;
        private short _version;

        public ValueTask<SocketResponse> Task => new(this, _version);

        public void Reset(CancellationToken cancellationToken)
        {
            _core.Reset();
            _version++;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(static state =>
                {
                    var pending = (PendingSocketRequest)state!;
                    pending.TrySetException(new OperationCanceledException());
                }, this);
            }
        }

        public void TrySetResult(SocketResponse result) => _core.SetResult(result);
        public void TrySetException(Exception error) => _core.SetException(error);

        SocketResponse IValueTaskSource<SocketResponse>.GetResult(short token) => _core.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource<SocketResponse>.GetStatus(short token) => _core.GetStatus(token);
        void IValueTaskSource<SocketResponse>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
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

        public ValueTask<SocketResponse> WaitAsync(int cid, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            var pending = new PendingSocketRequest();
            if (!_pending.TryAdd(cid, pending))
            {
                return new ValueTask<SocketResponse>(Task.FromException<SocketResponse>(new InvalidOperationException($"Duplicate pending cid {cid}.")));
            }

            pending.Reset(cancellationToken);
            if (timeoutMilliseconds > 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                var capturedCid = cid;
                _ = Task.Delay(timeoutMilliseconds).ContinueWith(static (t, state) =>
                {
                    if (t.IsCanceled)
                    {
                        return;
                    }

                    var (hub, pendingCid) = ((SocketCorrelationHub, int))state!;
                    if (hub._pending.TryRemove(pendingCid, out var p))
                    {
                        p.TrySetException(new TimeoutException("The socket timed out while waiting for a response."));
                    }
                }, (this, capturedCid), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            return pending.Task;
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
    }
}
