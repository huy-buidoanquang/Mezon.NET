using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private const int MaxBatchSize = 256;
        private int _disposed;
        private int _pendingCount;

        /// <summary>Failure of a batch not yet reported to a flush; used only by the pump loop.</summary>
        private Exception? _unreportedFailure;

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
                    DrainBatch();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                // Anything still queued will not be written; release flush waiters instead of leaving them hanging.
                while (_queue.TryDequeue(out var operation))
                {
                    if (operation is FlushOperation flush)
                    {
                        flush.Completion.TrySetCanceled();
                    }
                    else
                    {
                        Interlocked.Decrement(ref _pendingCount);
                    }
                }
            }
        }

        /// <summary>
        /// Writes up to <see cref="MaxBatchSize"/> queued operations in one transaction. A failed batch is rolled back
        /// and reported to the next flush; the pump keeps running.
        /// </summary>
        private void DrainBatch()
        {
            var batch = new List<IWriteOperation>(capacity: 32);
            while (batch.Count < MaxBatchSize && _queue.TryDequeue(out var next))
            {
                batch.Add(next);
            }

            if (batch.Count == 0)
            {
                return;
            }

            var writes = 0;
            foreach (var operation in batch)
            {
                if (operation is not FlushOperation)
                {
                    writes++;
                }
            }

            try
            {
                // Disposing an uncommitted transaction rolls it back.
                using var transaction = _connection.BeginTransaction();
                foreach (var operation in batch)
                {
                    if (operation is not FlushOperation)
                    {
                        operation.Execute(_connection, transaction);
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                _unreportedFailure = ex;
            }
            finally
            {
                // Each write leaves the pending count exactly once, whether it was committed or rolled back.
                Interlocked.Add(ref _pendingCount, -writes);
            }

            foreach (var operation in batch)
            {
                if (operation is not FlushOperation flush)
                {
                    continue;
                }

                if (_unreportedFailure is { } failure)
                {
                    flush.Completion.TrySetException(failure);
                }
                else
                {
                    flush.Completion.TrySetResult(true);
                }
            }

            if (batch.Exists(static operation => operation is FlushOperation))
            {
                _unreportedFailure = null;
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
