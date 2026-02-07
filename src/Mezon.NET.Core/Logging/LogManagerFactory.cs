using System;
using System.Threading.Tasks;

namespace Mezon.NET.Logging
{
    /// <summary>
    ///     Factory class for creating and configuring LogManager instances with various logging outputs.
    /// </summary>
    public static class LogManagerFactory
    {
        /// <summary>
        ///     Creates a LogManager with file and console logging enabled.
        /// </summary>
        /// <param name="logSeverity">The minimum log severity level.</param>
        /// <param name="logFilePath">The path to the log file. If null, uses default naming.</param>
        /// <param name="enableConsole">Whether to enable console logging.</param>
        /// <param name="enableFile">Whether to enable file logging.</param>
        /// <returns>A tuple containing the configured LogManager and FileLogWriter (if enabled).</returns>
        public static (LogManager LogManager, FileLogWriter? FileWriter) CreateWithLogging(
            LogSeverity logSeverity = LogSeverity.Info,
            string? logFilePath = null,
            bool enableConsole = true,
            bool enableFile = true)
        {
            var logManager = new LogManager(logSeverity);
            FileLogWriter? fileWriter = null;

            if (enableConsole)
            {
                logManager.AddConsoleLogging();
            }

            if (enableFile)
            {
                fileWriter = logManager.AddFileLogging(logFilePath, append: true);
            }

            return (logManager, fileWriter);
        }
    }
}
