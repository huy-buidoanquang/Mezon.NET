using System;
using System.Text;

namespace Mezon.Net.Logging
{
    /// <summary>
    ///     Provides a message object used for logging purposes.
    /// </summary>
    public struct LogMessage
    {
        /// <summary>
        ///     Gets the level of the log entry.
        /// </summary>
        /// <returns>
        ///     A <see cref="LogLevel"/> enum to indicate the severeness of the incident or event.
        /// </returns>
        public LogLevel Level { get; }
        /// <summary>
        ///     Gets the source of the log entry.
        /// </summary>
        /// <returns>
        ///     A string representing the source of the log entry.
        /// </returns>
        public string Source { get; }
        /// <summary>
        ///     Gets the message of this log entry.
        /// </summary>
        /// <returns>
        ///     A string containing the message of this log entry.
        /// </returns>
        public string Message { get; }
        /// <summary>
        ///     Gets the exception of this log entry.
        /// </summary>
        /// <returns>
        ///     An <see cref="Exception"/> object associated with an incident; otherwise <see langword="null"/>.
        /// </returns>
        public Exception? Exception { get; }

        /// <summary>
        ///     Initializes a new <see cref="LogMessage"/> struct with the level, source, message of the event, and
        ///     optionally, an exception.
        /// </summary>
        /// <param name="level">The level of the event.</param>
        /// <param name="source">The source of the event.</param>
        /// <param name="message">The message of the event.</param>
        /// <param name="exception">The exception of the event.</param>
        public LogMessage(LogLevel level, string source, string message, Exception? exception = null)
        {
            Level = level;
            Source = source;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => ToString();

        public string ToString(StringBuilder? builder = null, bool fullException = true, bool prependTimestamp = true, DateTimeKind timestampKind = DateTimeKind.Local, int? padSource = 11)
        {
            string sourceName = Source;
            string message = Message;
            string? exMessage = fullException ? Exception?.ToString() : Exception?.Message;

            int maxLength = 1 +
                (prependTimestamp ? 18 : 0) + 2 +
                (padSource.HasValue ? padSource.Value : sourceName?.Length ?? 0) + 1 +
                (message?.Length ?? 0) +
                (exMessage?.Length ?? 0) + 3;

            if (builder == null)
            {
                builder = new StringBuilder(maxLength);
            }
            else
            {
                builder.Clear();
                builder.EnsureCapacity(maxLength);
            }

            if (prependTimestamp)
            {
                DateTimeOffset now;
                if (timestampKind == DateTimeKind.Utc)
                {
                    now = DateTimeOffset.UtcNow;
                }
                else
                {
                    now = DateTimeOffset.Now;
                }

                string format = "yyyy-MM-dd HH:mm:ss";
                builder.Append(now.ToString(format));
                builder.Append(' ');
            }
            if (sourceName != null)
            {
                if (padSource.HasValue)
                {
                    if (sourceName.Length < padSource.Value)
                    {
                        builder.Append(sourceName);
                        builder.Append(' ', padSource.Value - sourceName.Length);
                    }
                    else if (sourceName.Length > padSource.Value)
                    {
                        builder.Append(sourceName.Substring(0, padSource.Value));
                    }
                    else
                    {
                        builder.Append(sourceName);
                    }
                }
                builder.Append(' ');
            }
            if (!string.IsNullOrEmpty(Message))
            {
                for (int i = 0; i < message?.Length; i++)
                {
                    //Strip control chars
                    char c = message[i];
                    if (!char.IsControl(c))
                    {
                        builder.Append(c);
                    }
                }
            }
            if (exMessage != null)
            {
                if (!string.IsNullOrEmpty(Message))
                {
                    builder.Append(':');
                    builder.AppendLine();
                }
                builder.Append(exMessage);
            }

            return builder.ToString();
        }
    }
}
