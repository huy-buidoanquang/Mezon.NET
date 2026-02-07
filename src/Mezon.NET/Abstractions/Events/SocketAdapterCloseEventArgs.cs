using System.Net.WebSockets;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents the event arguments for a WebSocket close event, providing details about the closure.
    /// This corresponds to the DOM CloseEvent interface.   
    /// </summary>
    public class SocketAdapterCloseEventArgs : SocketAdapterEventArgs
    {
        /// <summary>
        /// Gets a boolean that indicates whether the connection was closed cleanly.
        /// </summary>
        public bool WasClean { get; }

        /// <summary>
        /// Gets the WebSocket connection close code sent by the server.
        /// </summary>
        public int Code { get; }

        /// <summary>
        /// Gets a string indicating the reason the server closed the connection.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseEvent"/> class.
        /// </summary>
        public SocketAdapterCloseEventArgs(bool wasClean, int code, string reason, ClientWebSocket target) : base("close", target)
        {
            WasClean = wasClean;
            Code = code;
            Reason = reason;
        }
    }
}
