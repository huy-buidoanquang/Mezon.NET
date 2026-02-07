using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.NET.SDK
{
    /// <summary>
    /// A high-performance, thread-safe throttle queue that limits the number of operations per second.
    /// Automatically manages task execution to prevent rate limit violations.
    /// </summary>
    public class AsyncThrottleQueue : IDisposable
    {
        private const int DefaultMaxPerSecond = 80;
        private const int CleanupIntervalMs = 10;

        private readonly ConcurrentQueue<QueuedTask> _queue;
        private readonly ConcurrentBag<long> _timestamps;
        private readonly int _maxPerSecond;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the AsyncThrottleQueue class
        /// </summary>
        /// <param name="maxPerSecond">Maximum number of tasks to execute per second (default: 80)</param>
        public AsyncThrottleQueue(int maxPerSecond = DefaultMaxPerSecond)
        {
            if (maxPerSecond <= 0)
            {
                throw new ArgumentException("Max per second must be greater than 0", nameof(maxPerSecond));
            }

            _maxPerSecond = maxPerSecond;
            _queue = new ConcurrentQueue<QueuedTask>();
            _timestamps = new ConcurrentBag<long>();
            _semaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            // Start the background processing loop
            _processingTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Gets the current number of pending tasks in the queue
        /// </summary>
        public int QueueCount => _queue.Count;

        /// <summary>
        /// Gets the current number of tasks executed in the last second
        /// </summary>
        public int CurrentRate
        {
            get
            {
                CleanupTimestamps();
                return _timestamps.Count;
            }
        }

        /// <summary>
        /// Gets whether the queue is currently at maximum capacity
        /// </summary>
        public bool IsAtMaxRate => CurrentRate >= _maxPerSecond;

        /// <summary>
        /// Enqueues a task to be executed within the rate limit
        /// </summary>
        /// <typeparam name="T">The return type of the task</typeparam>
        /// <param name="taskFactory">Factory function that creates the task to execute</param>
        /// <returns>A task that completes when the enqueued task completes</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue has been disposed</exception>
        public Task<T> EnqueueAsync<T>(Func<Task<T>> taskFactory)
        {
            ThrowIfDisposed();

            if (taskFactory == null)
            {
                throw new ArgumentNullException(nameof(taskFactory));
            }

            var tcs = new TaskCompletionSource<T>();
            var queuedTask = new QueuedTask(async () =>
            {
                try
                {
                    var result = await taskFactory().ConfigureAwait(false);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            _queue.Enqueue(queuedTask);
            return tcs.Task;
        }

        /// <summary>
        /// Enqueues a task that returns void to be executed within the rate limit
        /// </summary>
        /// <param name="taskFactory">Factory function that creates the task to execute</param>
        /// <returns>A task that completes when the enqueued task completes</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue has been disposed</exception>
        public Task EnqueueAsync(Func<Task> taskFactory)
        {
            ThrowIfDisposed();

            if (taskFactory == null)
            {
                throw new ArgumentNullException(nameof(taskFactory));
            }

            var tcs = new TaskCompletionSource<bool>();
            var queuedTask = new QueuedTask(async () =>
            {
                try
                {
                    await taskFactory().ConfigureAwait(false);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            _queue.Enqueue(queuedTask);
            return tcs.Task;
        }

        /// <summary>
        /// Enqueues a synchronous action to be executed within the rate limit
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <returns>A task that completes when the action completes</returns>
        public Task EnqueueAsync(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return EnqueueAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Enqueues a synchronous function to be executed within the rate limit
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="func">The function to execute</param>
        /// <returns>A task that completes with the function result</returns>
        public Task<T> EnqueueAsync<T>(Func<T> func)
        {
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            return EnqueueAsync(() => Task.FromResult(func()));
        }

        /// <summary>
        /// Waits for all pending tasks to complete
        /// </summary>
        /// <param name="timeout">Optional timeout</param>
        /// <returns>True if all tasks completed within the timeout; otherwise false</returns>
        public async Task<bool> WaitForCompletionAsync(TimeSpan? timeout = null)
        {
            var startTime = DateTime.UtcNow;
            var timeoutTime = timeout.HasValue ? startTime.Add(timeout.Value) : DateTime.MaxValue;

            while (_queue.Count > 0)
            {
                if (DateTime.UtcNow >= timeoutTime)
                {
                    return false;
                }

                await Task.Delay(CleanupIntervalMs).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Clears all pending tasks from the queue
        /// </summary>
        public void ClearQueue()
        {
            while (_queue.TryDequeue(out _))
            { }
        }

        /// <summary>
        /// The main processing loop that executes tasks within the rate limit
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Clean up old timestamps
                    CleanupTimestamps();

                    // Check if we can process a task
                    if (_queue.TryPeek(out _) && _timestamps.Count < _maxPerSecond)
                    {
                        if (_queue.TryDequeue(out var queuedTask))
                        {
                            // Record timestamp
                            _timestamps.Add(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                            // Execute task without awaiting (fire and forget within rate limit)
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await queuedTask.ExecuteAsync().ConfigureAwait(false);
                                }
                                catch
                                {
                                    // Errors are handled in the TaskCompletionSource
                                }
                            }, _cancellationTokenSource.Token);
                        }
                    }

                    // Small delay to prevent CPU spinning
                    await Task.Delay(CleanupIntervalMs, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when disposing
                    break;
                }
                catch
                {
                    // Continue processing even if there's an error
                }
            }
        }

        /// <summary>
        /// Removes timestamps older than 1 second
        /// </summary>
        private void CleanupTimestamps()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cutoff = now - 1000;

            // Convert to list, filter, and replace
            var validTimestamps = _timestamps.Where(t => t >= cutoff).ToList();

            // Clear and re-add (ConcurrentBag doesn't have efficient removal)
            while (_timestamps.TryTake(out _))
            { }
            foreach (var timestamp in validTimestamps)
            {
                _timestamps.Add(timestamp);
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the queue has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncThrottleQueue));
            }
        }

        /// <summary>
        /// Disposes the queue and cancels all pending operations
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _cancellationTokenSource?.Cancel();

            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected when cancelling
            }

            _cancellationTokenSource?.Dispose();
            _semaphore?.Dispose();
        }

        /// <summary>
        /// Internal class representing a queued task
        /// </summary>
        private class QueuedTask
        {
            private readonly Func<Task> _taskFactory;

            public QueuedTask(Func<Task> taskFactory)
            {
                _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
            }

            public Task ExecuteAsync() => _taskFactory();
        }
    }
}
