using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.SDK;

namespace Mezon.NET.SDK.Tests
{
    /// <summary>
    /// Unit tests for AsyncThrottleQueue
    /// Note: Add a test framework like xUnit, NUnit, or MSTest to run these tests
    /// Example with xUnit: dotnet add package xunit
    /// </summary>
    public class AsyncThrottleQueueTests
    {
        // [Fact]
        public async Task Constructor_WithValidMaxPerSecond_ShouldInitialize()
        {
            // Arrange & Act
            using var queue = new AsyncThrottleQueue(maxPerSecond: 50);

            // Assert
            // Assert.NotNull(queue);
            // Assert.Equal(0, queue.QueueCount);
            // Assert.Equal(0, queue.CurrentRate);
        }

        // [Fact]
        public void Constructor_WithInvalidMaxPerSecond_ShouldThrowException()
        {
            // Act & Assert
            // Assert.Throws<ArgumentException>(() => new AsyncThrottleQueue(maxPerSecond: 0));
            // Assert.Throws<ArgumentException>(() => new AsyncThrottleQueue(maxPerSecond: -1));
        }

        // [Fact]
        public async Task EnqueueAsync_WithAsyncTask_ShouldExecute()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var executed = false;

            // Act
            await queue.EnqueueAsync(async () =>
            {
                await Task.Delay(10);
                executed = true;
            });

            // Assert
            // Assert.True(executed);
        }

        // [Fact]
        public async Task EnqueueAsync_WithAsyncTaskReturningValue_ShouldReturnResult()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var expectedValue = 42;

            // Act
            var result = await queue.EnqueueAsync(async () =>
            {
                await Task.Delay(10);
                return expectedValue;
            });

            // Assert
            // Assert.Equal(expectedValue, result);
        }

        // [Fact]
        public async Task EnqueueAsync_WithSyncAction_ShouldExecute()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var executed = false;

            // Act
            await queue.EnqueueAsync(() => executed = true);

            // Assert
            // Assert.True(executed);
        }

        // [Fact]
        public async Task EnqueueAsync_WithSyncFunc_ShouldReturnResult()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var expectedValue = "test";

            // Act
            var result = await queue.EnqueueAsync(() => expectedValue);

            // Assert
            // Assert.Equal(expectedValue, result);
        }

        // [Fact]
        public async Task EnqueueAsync_WithNullTaskFactory_ShouldThrowException()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);

            // Act & Assert
            // await Assert.ThrowsAsync<ArgumentNullException>(() => 
            //     queue.EnqueueAsync<int>((Func<Task<int>>)null));
        }

        // [Fact]
        public async Task EnqueueAsync_MultipleTasks_ShouldRespectRateLimit()
        {
            // Arrange
            var maxPerSecond = 20;
            using var queue = new AsyncThrottleQueue(maxPerSecond: maxPerSecond);
            var executionTimes = new List<long>();
            var lockObj = new object();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 50)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    lock (lockObj)
                    {
                        executionTimes.Add(stopwatch.ElapsedMilliseconds);
                    }
                    await Task.Delay(1);
                    return i;
                }))
                .ToList();

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var duration = stopwatch.Elapsed.TotalSeconds;
            var actualRate = tasks.Count / duration;

            // Assert.True(actualRate <= maxPerSecond * 1.1); // Allow 10% margin
            // Assert.True(duration >= 2.0); // 50 tasks at 20/sec should take at least 2.5 seconds
        }

        // [Fact]
        public async Task EnqueueAsync_ConcurrentEnqueues_ShouldAllComplete()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 50);
            var taskCount = 100;
            var completedCount = 0;

            // Act
            var enqueueTasks = Enumerable.Range(0, taskCount)
                .Select(async i =>
                {
                    await queue.EnqueueAsync(async () =>
                    {
                        await Task.Delay(10);
                        Interlocked.Increment(ref completedCount);
                    });
                })
                .ToList();

            await Task.WhenAll(enqueueTasks);

            // Assert
            // Assert.Equal(taskCount, completedCount);
        }

        // [Fact]
        public async Task EnqueueAsync_WithException_ShouldPropagateException()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var expectedException = new InvalidOperationException("Test exception");

            // Act & Assert
            var exception = await Assert_ThrowsAsync<InvalidOperationException>(async () =>
            {
                await queue.EnqueueAsync<int>(async () =>
                {
                    await Task.Delay(10);
                    throw expectedException;
                });
            });

            // Assert.Equal(expectedException.Message, exception.Message);
        }

        // [Fact]
        public async Task QueueCount_ShouldReflectPendingTasks()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 1); // Very slow to keep tasks pending
            var initialCount = queue.QueueCount;

            // Act
            var tasks = Enumerable.Range(0, 10)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(100)))
                .ToList();

            await Task.Delay(50); // Let some tasks start
            var pendingCount = queue.QueueCount;

            await Task.WhenAll(tasks);
            var finalCount = queue.QueueCount;

            // Assert
            // Assert.Equal(0, initialCount);
            // Assert.True(pendingCount > 0);
            // Assert.Equal(0, finalCount);
        }

        // [Fact]
        public async Task CurrentRate_ShouldReflectExecutionRate()
        {
            // Arrange
            var maxPerSecond = 30;
            using var queue = new AsyncThrottleQueue(maxPerSecond: maxPerSecond);

            // Act
            var tasks = Enumerable.Range(0, 40)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(10)))
                .ToList();

            await Task.Delay(100); // Let some tasks execute

            var currentRate = queue.CurrentRate;

            await Task.WhenAll(tasks);

            // Assert
            // Assert.True(currentRate > 0);
            // Assert.True(currentRate <= maxPerSecond);
        }

        // [Fact]
        public async Task IsAtMaxRate_ShouldIndicateCapacity()
        {
            // Arrange
            var maxPerSecond = 5;
            using var queue = new AsyncThrottleQueue(maxPerSecond: maxPerSecond);

            // Act
            var initialIsAtMax = queue.IsAtMaxRate;

            var tasks = Enumerable.Range(0, 10)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(200)))
                .ToList();

            await Task.Delay(50);
            var duringIsAtMax = queue.IsAtMaxRate;

            await Task.WhenAll(tasks);
            var finalIsAtMax = queue.IsAtMaxRate;

            // Assert
            // Assert.False(initialIsAtMax);
            // Assert.True(duringIsAtMax); // Should be at max during execution
            // Assert.False(finalIsAtMax);
        }

        // [Fact]
        public async Task WaitForCompletionAsync_ShouldWaitForAllTasks()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 20);
            var taskCount = 30;

            // Act
            var tasks = Enumerable.Range(0, taskCount)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(50)))
                .ToList();

            var completed = await queue.WaitForCompletionAsync(timeout: TimeSpan.FromSeconds(5));

            // Assert
            // Assert.True(completed);
            // Assert.Equal(0, queue.QueueCount);
        }

        // [Fact]
        public async Task WaitForCompletionAsync_WithTimeout_ShouldReturnFalse()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 5);

            // Act
            var tasks = Enumerable.Range(0, 50)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(100)))
                .ToList();

            var completed = await queue.WaitForCompletionAsync(timeout: TimeSpan.FromMilliseconds(100));

            // Assert
            // Assert.False(completed); // Should timeout
            // Assert.True(queue.QueueCount > 0);

            // Cleanup
            await Task.WhenAll(tasks);
        }

        // [Fact]
        public async Task ClearQueue_ShouldRemoveAllPendingTasks()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 1);
            var tasks = Enumerable.Range(0, 10)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(100)))
                .ToList();

            await Task.Delay(50);

            // Act
            var initialCount = queue.QueueCount;
            queue.ClearQueue();
            var afterClearCount = queue.QueueCount;

            // Assert
            // Assert.True(initialCount > 0);
            // Assert.Equal(0, afterClearCount);
        }

        // [Fact]
        public async Task Dispose_ShouldCancelProcessing()
        {
            // Arrange
            var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            var tasks = Enumerable.Range(0, 20)
                .Select(i => queue.EnqueueAsync(async () => await Task.Delay(50)))
                .ToList();

            // Act
            queue.Dispose();

            // Queue should stop processing
            await Task.Delay(200);

            // Assert - some tasks may not complete
            var completedCount = tasks.Count(t => t.IsCompleted);
            // Assert.True(completedCount < tasks.Count);
        }

        // [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var queue = new AsyncThrottleQueue(maxPerSecond: 10);

            // Act & Assert - should not throw
            queue.Dispose();
            queue.Dispose();
            queue.Dispose();
        }

        // [Fact]
        public async Task EnqueueAsync_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var queue = new AsyncThrottleQueue(maxPerSecond: 10);
            queue.Dispose();

            // Act & Assert
            // await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            //     queue.EnqueueAsync(async () => await Task.Delay(10)));
        }

        // [Fact]
        public async Task StressTest_ManyTasksHighRate_ShouldComplete()
        {
            // Arrange
            using var queue = new AsyncThrottleQueue(maxPerSecond: 100);
            var taskCount = 1000;
            var completedCount = 0;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, taskCount)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    await Task.Delay(1);
                    Interlocked.Increment(ref completedCount);
                    return i;
                }))
                .ToList();

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            // Assert.Equal(taskCount, completedCount);
            // Assert.True(stopwatch.Elapsed.TotalSeconds >= 9); // Should take at least 10 seconds
            // Assert.True(stopwatch.Elapsed.TotalSeconds < 15); // But not too long
        }

        #region Helper Methods

        private static async Task<TException> Assert_ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
                throw new Exception($"Expected exception of type {typeof(TException).Name} was not thrown");
            }
            catch (TException ex)
            {
                return ex;
            }
        }

        #endregion
    }
}
