using System;
using System.IO;
using System.Threading.Tasks;

namespace Mezon.NET.Logging
{
    /// <summary>
    ///     Provides file-based logging functionality for Mezon log messages.
    /// </summary>
    public class FileLogWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the FileLogWriter class.
        /// </summary>
        /// <param name="filePath">The path to the log file. If null, defaults to "mezon-{date}.log"</param>
        /// <param name="append">Whether to append to existing file or overwrite it.</param>
        public FileLogWriter(string? filePath = null, bool append = true)
        {
            filePath ??= $"mezon-{DateTime.Now:yyyy-MM-dd}.log";

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(filePath, append) { AutoFlush = true };
        }

        /// <summary>
        ///     Writes a log message to the file.
        /// </summary>
        public Task WriteLogAsync(LogMessage message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileLogWriter));
            }

            lock (_lock)
            {
                var formattedMessage = message.ToString(
                    prependTimestamp: true,
                    timestampKind: DateTimeKind.Local,
                    padSource: 20,
                    fullException: true
                );

                _writer.WriteLine(formattedMessage);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (_lock)
            {
                _writer?.Dispose();
            }
        }
    }
}
