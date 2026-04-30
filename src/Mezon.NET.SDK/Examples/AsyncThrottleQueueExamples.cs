using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mezon.Net.SDK.Examples
{
    /// <summary>
    /// Examples demonstrating the usage of AsyncThrottleQueue
    /// </summary>
    public static class AsyncThrottleQueueExamples
    {
        /// <summary>
        /// Basic throttle queue usage
        /// </summary>
        public static async Task BasicUsageExample()
        {
            Console.WriteLine("=== Basic Usage Example ===\n");

            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);

            var stopwatch = Stopwatch.StartNew();

            // Enqueue 25 tasks (should take ~2.5 seconds at 10/sec)
            var tasks = Enumerable.Range(1, 25)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    Console.WriteLine($"[{stopwatch.Elapsed.TotalSeconds:F2}s] Executing task {i}");
                    await Task.Delay(50); // Simulate work
                    return i;
                }))
                .ToList();

            Console.WriteLine($"Enqueued {tasks.Count} tasks");
            Console.WriteLine($"Queue count: {queue.QueueCount}");

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"\nCompleted {results.Length} tasks in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"Average rate: {results.Length / stopwatch.Elapsed.TotalSeconds:F2} tasks/second\n");
        }

        /// <summary>
        /// API rate limiting example
        /// </summary>
        public static async Task ApiRateLimitingExample()
        {
            Console.WriteLine("=== API Rate Limiting Example ===\n");

            // Simulate an API with 50 requests per second limit
            using var queue = new AsyncThrottleQueue(maxPerSecond: 50);
            using var httpClient = new HttpClient();

            var urls = Enumerable.Range(1, 100)
                .Select(i => $"https://jsonplaceholder.typicode.com/posts/{i}")
                .ToList();

            Console.WriteLine($"Fetching {urls.Count} URLs with 50 req/sec limit...");

            var stopwatch = Stopwatch.StartNew();
            var tasks = urls.Select(url => queue.EnqueueAsync(async () =>
            {
                try
                {
                    var response = await httpClient.GetStringAsync(url);
                    return response.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching {url}: {ex.Message}");
                    return 0;
                }
            })).ToList();

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            var successful = results.Count(r => r > 0);
            Console.WriteLine($"\nCompleted {successful}/{urls.Count} requests in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"Average rate: {results.Length / stopwatch.Elapsed.TotalSeconds:F2} requests/second\n");
        }

        /// <summary>
        /// Batch processing with monitoring example
        /// </summary>
        public static async Task BatchProcessingWithMonitoringExample()
        {
            Console.WriteLine("=== Batch Processing with Monitoring ===\n");

            using var queue = new AsyncThrottleQueue(maxPerSecond: 20);

            // Start a monitoring task
            var monitoringCts = new System.Threading.CancellationTokenSource();
            var monitoringTask = Task.Run(async () =>
            {
                while (!monitoringCts.Token.IsCancellationRequested)
                {
                    Console.WriteLine($"[Monitor] Queue: {queue.QueueCount} pending, " +
                                    $"Rate: {queue.CurrentRate}/{20} per second, " +
                                    $"At max: {queue.IsAtMaxRate}");
                    await Task.Delay(500, monitoringCts.Token);
                }
            }, monitoringCts.Token);

            // Process a large batch
            var batchSize = 100;
            var tasks = Enumerable.Range(1, batchSize)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    await Task.Delay(100); // Simulate processing
                    return $"Result_{i}";
                }))
                .ToList();

            Console.WriteLine($"Processing {batchSize} items...\n");

            var results = await Task.WhenAll(tasks);

            monitoringCts.Cancel();
            try
            {
                await monitoringTask;
            }
            catch { }

            Console.WriteLine($"\nProcessed {results.Length} items successfully\n");
        }

        /// <summary>
        /// Dynamic task enqueueing example
        /// </summary>
        public static async Task DynamicEnqueueingExample()
        {
            Console.WriteLine("=== Dynamic Enqueueing Example ===\n");

            using var queue = new AsyncThrottleQueue(maxPerSecond: 30);

            var completedCount = 0;
            var totalTasks = 50;
            var random = new Random();

            // Enqueue tasks dynamically over time
            var enqueueTask = Task.Run(async () =>
            {
                for (int i = 1; i <= totalTasks; i++)
                {
                    var taskId = i;
                    _ = queue.EnqueueAsync(async () =>
                    {
                        await Task.Delay(50);
                        System.Threading.Interlocked.Increment(ref completedCount);
                        Console.WriteLine($"Completed task {taskId} ({completedCount}/{totalTasks})");
                    });

                    // Add new tasks at irregular intervals
                    await Task.Delay(random.Next(50, 200));
                }
            });

            await enqueueTask;
            Console.WriteLine("\nAll tasks enqueued, waiting for completion...");

            // Wait for all tasks to complete
            await queue.WaitForCompletionAsync(timeout: TimeSpan.FromSeconds(10));

            Console.WriteLine($"\nTotal completed: {completedCount}/{totalTasks}\n");
        }

        /// <summary>
        /// Error handling example
        /// </summary>
        public static async Task ErrorHandlingExample()
        {
            Console.WriteLine("=== Error Handling Example ===\n");

            using var queue = new AsyncThrottleQueue(maxPerSecond: 10);

            var tasks = Enumerable.Range(1, 20)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    if (i % 5 == 0)
                    {
                        throw new InvalidOperationException($"Simulated error for task {i}");
                    }

                    await Task.Delay(50);
                    return i;
                }))
                .ToList();

            var successCount = 0;
            var errorCount = 0;

            foreach (var task in tasks)
            {
                try
                {
                    var result = await task;
                    successCount++;
                    Console.WriteLine($"? Task completed successfully: {result}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine($"? Task failed: {ex.Message}");
                }
            }

            Console.WriteLine($"\nResults: {successCount} successful, {errorCount} failed\n");
        }

        /// <summary>
        /// Comparison with unthrottled execution
        /// </summary>
        public static async Task PerformanceComparisonExample()
        {
            Console.WriteLine("=== Performance Comparison ===\n");

            var taskCount = 100;

            // Unthrottled execution
            Console.WriteLine("Running unthrottled...");
            var unthrottledStopwatch = Stopwatch.StartNew();
            var unthrottledTasks = Enumerable.Range(1, taskCount)
                .Select(async i =>
                {
                    await Task.Delay(10);
                    return i;
                })
                .ToList();
            await Task.WhenAll(unthrottledTasks);
            unthrottledStopwatch.Stop();

            // Throttled execution
            Console.WriteLine("Running throttled (50/sec)...");
            using var queue = new AsyncThrottleQueue(maxPerSecond: 50);
            var throttledStopwatch = Stopwatch.StartNew();
            var throttledTasks = Enumerable.Range(1, taskCount)
                .Select(i => queue.EnqueueAsync(async () =>
                {
                    await Task.Delay(10);
                    return i;
                }))
                .ToList();
            await Task.WhenAll(throttledTasks);
            throttledStopwatch.Stop();

            Console.WriteLine($"\nUnthrottled: {unthrottledStopwatch.Elapsed.TotalSeconds:F2}s " +
                            $"({taskCount / unthrottledStopwatch.Elapsed.TotalSeconds:F0} tasks/sec)");
            Console.WriteLine($"Throttled:   {throttledStopwatch.Elapsed.TotalSeconds:F2}s " +
                            $"({taskCount / throttledStopwatch.Elapsed.TotalSeconds:F0} tasks/sec)");
            Console.WriteLine($"\nThrottle overhead: {(throttledStopwatch.Elapsed - unthrottledStopwatch.Elapsed).TotalMilliseconds:F0}ms\n");
        }

        /// <summary>
        /// Message sending with rate limiting (Mezon SDK use case)
        /// </summary>
        public static async Task MessageSendingExample()
        {
            Console.WriteLine("=== Message Sending Example ===\n");

            // Simulate Mezon API rate limit (80 messages per second)
            using var queue = new AsyncThrottleQueue(maxPerSecond: 80);

            var channelId = "channel_123";
            var messages = Enumerable.Range(1, 200)
                .Select(i => $"Message {i}")
                .ToList();

            Console.WriteLine($"Sending {messages.Count} messages to channel {channelId}...");

            var stopwatch = Stopwatch.StartNew();
            var tasks = messages.Select((message, index) =>
                queue.EnqueueAsync(async () =>
                {
                    // Simulate API call
                    await Task.Delay(20);

                    if (index % 50 == 0)
                    {
                        Console.WriteLine($"Sent {index + 1} messages...");
                    }

                    return new
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        Content = message,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                })).ToList();

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"\nSent {results.Length} messages in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"Average rate: {results.Length / stopwatch.Elapsed.TotalSeconds:F2} messages/second");
            Console.WriteLine($"Within rate limit: {results.Length / stopwatch.Elapsed.TotalSeconds <= 80}\n");
        }

        /// <summary>
        /// Runs all examples
        /// </summary>
        public static async Task RunAllExamplesAsync()
        {
            await BasicUsageExample();
            await Task.Delay(1000);

            await BatchProcessingWithMonitoringExample();
            await Task.Delay(1000);

            await DynamicEnqueueingExample();
            await Task.Delay(1000);

            await ErrorHandlingExample();
            await Task.Delay(1000);

            await PerformanceComparisonExample();
            await Task.Delay(1000);

            await MessageSendingExample();

            Console.WriteLine("=== All Examples Completed ===");
        }
    }
}
