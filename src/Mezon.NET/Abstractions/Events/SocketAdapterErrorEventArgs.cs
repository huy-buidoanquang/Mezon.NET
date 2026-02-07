using System;
using System.Net.WebSockets;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents the event arguments for a WebSocket error event.
    /// </summary>
    public class SocketAdapterErrorEventArgs : SocketAdapterEventArgs
    {
        /// <summary>
        /// Gets the exception that caused the error.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets a human-readable error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEvent"/> class.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="message">The error message.</param>
        /// <param name="type">The type of the event (e.g., "error").</param>
        /// <param name="target">The target WebSocket instance.</param>
        public SocketAdapterErrorEventArgs(Exception exception, string message, ClientWebSocket target)
            : base("error", target)
        {
            Exception = exception;
            Message = message;
        }
    }
}
