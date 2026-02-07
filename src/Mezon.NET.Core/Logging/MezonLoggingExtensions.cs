using System;

namespace Mezon.NET.Logging
{
    /// <summary>
    ///     Extension methods for configuring Mezon logging.
    /// </summary>
    public static class MezonLoggingExtensions
    {
        /// <summary>
        ///     Adds a file log writer to the LogManager that persists logs to disk.
        /// </summary>
        /// <param name="logManager">The LogManager to attach the file writer to.</param>
        /// <param name="filePath">The path to the log file. If null, defaults to "mezon-{date}.log"</param>
        /// <param name="append">Whether to append to existing file or overwrite it.</param>
        /// <returns>The FileLogWriter instance for disposal management.</returns>
        public static FileLogWriter AddFileLogging(this LogManager logManager, string? filePath = null, bool append = true)
        {
            if (logManager == null)
            {
                throw new ArgumentNullException(nameof(logManager));
            }

            var fileWriter = new FileLogWriter(filePath, append);
            logManager.Message += fileWriter.WriteLogAsync;
            return fileWriter;
        }

        /// <summary>
        ///     Adds console logging to the LogManager.
        /// </summary>
        /// <param name="logManager">The LogManager to attach the console writer to.</param>
        /// <returns>The LogManager for chaining.</returns>
        public static LogManager AddConsoleLogging(this LogManager logManager)
        {
            if (logManager == null)
            {
                throw new ArgumentNullException(nameof(logManager));
            }

            logManager.Message += message =>
            {
                var formatted = message.ToString(
                    prependTimestamp: true,
                    timestampKind: DateTimeKind.Local,
                    padSource: 20,
                    fullException: true
                );

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = GetConsoleColor(message.Severity);
                Console.WriteLine(formatted);
                Console.ForegroundColor = originalColor;

                return System.Threading.Tasks.Task.CompletedTask;
            };

            return logManager;
        }

        private static ConsoleColor GetConsoleColor(LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Critical => ConsoleColor.Magenta,
                LogSeverity.Error => ConsoleColor.Red,
                LogSeverity.Warning => ConsoleColor.Yellow,
                LogSeverity.Info => ConsoleColor.White,
                LogSeverity.Verbose => ConsoleColor.Gray,
                LogSeverity.Debug => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };
        }
    }
}
