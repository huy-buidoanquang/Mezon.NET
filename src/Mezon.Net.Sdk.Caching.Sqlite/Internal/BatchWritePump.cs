using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Mezon.Net.Sdk.Caching.Sqlite.Internal
{
    internal sealed class BatchWritePump : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConcurrentQueue<IWriteOperation> _queue = new ConcurrentQueue<IWriteOperation>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _loop;
        private readonly TaskCompletionSource<bool> _started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposed;
        private int _pendingCount;

        internal BatchWritePump(SqliteConnection connection)
        {
            _connection = connection;
            _loop = Task.Run(RunAsync);
        }

        internal void Enqueue(IWriteOperation operation)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(BatchWritePump));
            }

            Interlocked.Increment(ref _pendingCount);
            _queue.Enqueue(operation);
            _signal.Release();
        }

        internal async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await RequestFlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task RequestFlushAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(new FlushOperation(tcs));
            _signal.Release();

            using var registration = cancellationToken.Register(static state =>
            {
                ((TaskCompletionSource<bool>)state!).TrySetCanceled();
            }, tcs);

            await tcs.Task.ConfigureAwait(false);
        }

        internal int PendingCount => Volatile.Read(ref _pendingCount);

        private async Task RunAsync()
        {
            _started.TrySetResult(true);
            var token = _cts.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await _signal.WaitAsync(token).ConfigureAwait(false);
                    await DrainQueueAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        private async Task DrainQueueAsync()
        {
            if (!_queue.TryDequeue(out var first))
            {
                return;
            }

            var batch = new System.Collections.Generic.List<IWriteOperation>(capacity: 32) { first };
            while (_queue.TryDequeue(out var next))
            {
                batch.Add(next);
            }

            var flushTargets = new System.Collections.Generic.List<TaskCompletionSource<bool>>(capacity: 1);
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var operation in batch)
                {
                    if (operation is FlushOperation flush)
                    {
                        flushTargets.Add(flush.Completion);
                        continue;
                    }

                    operation.Execute(_connection, transaction);
                    Interlocked.Decrement(ref _pendingCount);
                }

                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                foreach (var operation in batch)
                {
                    if (operation is FlushOperation)
                    {
                        continue;
                    }

                    Interlocked.Decrement(ref _pendingCount);
                }

                throw;
            }

            foreach (var flush in flushTargets)
            {
                flush.TrySetResult(true);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await _started.Task.ConfigureAwait(false);

            try
            {
                await RequestFlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }

            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _cts.Cancel();
            _signal.Release();
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _signal.Dispose();
            _cts.Dispose();
        }

        private sealed class FlushOperation : IWriteOperation
        {
            internal FlushOperation(TaskCompletionSource<bool> completion) => Completion = completion;

            internal TaskCompletionSource<bool> Completion { get; }

            public void Execute(SqliteConnection connection, SqliteTransaction transaction)
            {
            }
        }
    }

    internal interface IWriteOperation
    {
        void Execute(SqliteConnection connection, SqliteTransaction transaction);
    }
}
