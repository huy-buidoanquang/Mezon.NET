using System;
using System.Net.WebSockets;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents the base class for WebSocket event arguments.
    /// </summary>
    public class SocketAdapterEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a string representing the event's type (e.g., "open", "close").
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the WebSocket instance that is the target of this event.
        /// </summary>
        public ClientWebSocket Target { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="type">The type of the event.</param>
        /// <param name="target">The target WebSocket instance.</param>
        public SocketAdapterEventArgs(string type, ClientWebSocket target)
        {
            Type = type;
            Target = target;
        }
    }
}
