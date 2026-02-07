//using Mezon.NET.Api;
//using Mezon.NET.Logging;
//using System;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;

//namespace Mezon.NET.Example
//{
//    /// <summary>
//    ///     Examples demonstrating different ways to configure logging with Mezon.NET
//    /// </summary>
//    public static class LoggingExamples
//    {
//        /// <summary>
//        ///     Example 1: Basic file and console logging with MezonClient
//        /// </summary>
//        public static async Task BasicLoggingExample()
//        {
//            // Create a LogManager with file and console logging
//            var (logManager, fileWriter) = LogManagerFactory.CreateWithLogging(
//                logSeverity: LogSeverity.Info,
//                logFilePath: "logs/mezon.log",
//                enableConsole: true,
//                enableFile: true
//            );

//            try
//            {
//                var config = new MezonApiClientConfiguration("clientId", "clientSecret")
//                {
//                    LogLevel = LogSeverity.Verbose
//                };

//                // Create client with shared log manager
//                var client = new MezonClient(config, logManager);

//                await client.LoginAsync();

//                // Your application logic here
//            }
//            finally
//            {
//                // Clean up
//                fileWriter?.Dispose();
//            }
//        }

//        /// <summary>
//        ///     Example 2: Custom log handling with event subscription
//        /// </summary>
//        public static async Task CustomLogHandlingExample()
//        {
//            var logManager = new LogManager(LogSeverity.Debug);

//            // Subscribe to log events for custom processing
//            logManager.Message += async (message) =>
//            {
//                // Custom log processing - e.g., send to external logging service
//                Console.WriteLine($"[{message.Severity}] {message.Source}: {message.Message}");

//                // You could also:
//                // - Send to Elasticsearch
//                // - Store in database
//                // - Send alerts for Critical/Error logs
//                // - etc.

//                await Task.CompletedTask;
//            };

//            var config = new MezonApiClientConfiguration("clientId", "clientSecret");
//            var client = new MezonClient(config, logManager);

//            await client.LoginAsync();
//        }

//        /// <summary>
//        ///     Example 3: Console-only logging for quick debugging
//        /// </summary>
//        public static async Task ConsoleOnlyLoggingExample()
//        {
//            var logManager = new LogManager(LogSeverity.Debug)
//                .AddConsoleLogging();

//            var config = new MezonApiClientConfiguration("clientId", "clientSecret");
//            var client = new MezonClient(config, logManager);

//            await client.LoginAsync();
//        }

//        /// <summary>
//        ///     Example 4: Multiple log outputs (file + console + custom handler)
//        /// </summary>
//        public static async Task MultipleLogOutputsExample()
//        {
//            var logManager = new LogManager(LogSeverity.Info);

//            // Add console logging
//            logManager.AddConsoleLogging();

//            // Add file logging
//            var fileWriter = logManager.AddFileLogging("logs/mezon-detailed.log");

//            // Add custom handler for errors only
//            logManager.Message += async (message) =>
//            {
//                if (message.Severity <= LogSeverity.Error)
//                {
//                    // Send error notifications, alerts, etc.
//                    Console.WriteLine($"ERROR ALERT: {message.Message}");
//                }
//                await Task.CompletedTask;
//            };

//            try
//            {
//                var config = new MezonApiClientConfiguration("clientId", "clientSecret");
//                var client = new MezonClient(config, logManager);

//                await client.LoginAsync();
//            }
//            finally
//            {
//                fileWriter?.Dispose();
//            }
//        }

//        /// <summary>
//        ///     Example 5: Daily rotating log files
//        /// </summary>
//        public static async Task DailyRotatingLogsExample()
//        {
//            var logManager = new LogManager(LogSeverity.Info);

//            // Create log file with date in name (will create new file each day)
//            var logFileName = $"logs/mezon-{DateTime.Now:yyyy-MM-dd}.log";
//            var fileWriter = logManager.AddFileLogging(logFileName, append: true);

//            try
//            {
//                var config = new MezonApiClientConfiguration("clientId", "clientSecret");
//                var client = new MezonClient(config, logManager);

//                await client.LoginAsync();
//            }
//            finally
//            {
//                fileWriter?.Dispose();
//            }
//        }

//        /// <summary>
//        ///     Example 6: Clean up old log files
//        /// </summary>
//        public static void CleanUpOldLogs(int daysToKeep = 7)
//        {
//            var logDirectory = new DirectoryInfo("logs");
//            if (!logDirectory.Exists)
//                return;

//            var oldLogs = logDirectory.GetFiles("mezon-*.log")
//                .Where(f => f.CreationTime < DateTime.Now.AddDays(-daysToKeep));

//            foreach (var file in oldLogs)
//            {
//                try
//                {
//                    file.Delete();
//                    Console.WriteLine($"Deleted old log file: {file.Name}");
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Failed to delete {file.Name}: {ex.Message}");
//                }
//            }
//        }

//        /// <summary>
//        ///     Example 7: Separate error log file
//        /// </summary>
//        public static async Task SeparateErrorLogExample()
//        {
//            var logManager = new LogManager(LogSeverity.Info);

//            // All logs
//            var allLogsWriter = logManager.AddFileLogging("logs/all.log");

//            // Errors only
//            using var errorStream = new FileLogWriter("logs/errors-only.log");
//            logManager.Message += async (message) =>
//            {
//                if (message.Severity <= LogSeverity.Error)
//                {
//                    await errorStream.WriteLogAsync(message);
//                }
//            };

//            try
//            {
//                var config = new MezonApiClientConfiguration("clientId", "clientSecret");
//                var client = new MezonClient(config, logManager);

//                await client.LoginAsync();
//            }
//            finally
//            {
//                allLogsWriter?.Dispose();
//            }
//        }
//    }
//}
