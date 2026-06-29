using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions.Events;

namespace Mezon.NET.Abstractions
{
    /// <summary>
    /// An adapter interface for a WebSocket connection.
    /// </summary>
    public interface IWebSocketAdapter : IAsyncDisposable
    {
        /// <summary>
        /// Dispatched when the web socket connection is successfully opened.
        /// </summary>
        event EventHandler<SocketAdapterOpenEventArgs>? Opened;

        /// <summary>
        /// Dispatched when the web socket connection closes.
        /// </summary>
        event EventHandler<SocketAdapterCloseEventArgs>? Closed;

        /// <summary>
        /// Dispatched when the web socket receives a message.
        /// </summary>
        event EventHandler<SocketAdapterMessageEventArgs>? MessageReceived;

        /// <summary>
        /// Dispatched when the web socket encounters an error.
        /// </summary>
        event EventHandler<SocketAdapterErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Checks if the socket connection is currently open.
        /// </summary>
        /// <returns>True if the socket is open, otherwise false.</returns>
        bool IsOpen();

        /// <summary>
        /// Establishes a new connection to the web socket server.
        /// </summary>
        /// <param name="scheme">The connection scheme (e.g., "ws" or "wss").</param>
        /// <param name="host">The server hostname.</param>
        /// <param name="port">The server port.</param>
        /// <param name="createStatus">A flag for creating a status.</param>
        /// <param name="token">The authentication token.</param>
        Task ConnectAsync(string scheme, string host, int port, bool createStatus, string token, CancellationToken cancellation = default);

        /// <summary>
        /// Closes the active web socket connection.
        /// </summary>
        Task CloseAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Sends a message over the web socket.
        /// </summary>
        /// <param name="message">The data to send. Typically a serializable object.</param>
        Task SendAsync(object message, CancellationToken cancellation = default);
    }
}
