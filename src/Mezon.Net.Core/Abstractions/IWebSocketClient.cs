using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Abstractions
{
    public delegate IWebSocketClient WebSocketClientProvider();

    public interface IWebSocketClient : IDisposable
    {
        /// <summary>
        /// Event raised when a binary message is received
        /// </summary>
        event Func<ReadOnlyMemory<byte>, ValueTask> MessageReceived;

        /// <summary>
        /// Event raised when the WebSocket connection is closed
        /// </summary>
        event Func<Exception, Task> Closed;

        /// <summary>
        /// Event raised when the WebSocket connection is opened
        /// </summary>
        event Func<Task> Opened;

        /// <summary>
        /// Event raised when the WebSocket connection is ready
        /// </summary>
        event Func<Task> Ready;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event Func<Exception, Task> ErrorOccurred;

        /// <summary>
        /// Sets a header to be included in the WebSocket handshake request.
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value</param>
        /// <remarks>
        /// This method should be called before connecting to the WebSocket server.
        /// </remarks>
        void SetHeader(string key, string value);

        /// <summary>
        /// Sets the cancellation token to be used for WebSocket operations.
        /// </summary>
        /// <param name="cancelToken">The cancellation token</param>
        /// <remarks>
        /// This method should be called before connecting to the WebSocket server.
        /// </remarks>
        void SetCancelToken(CancellationToken cancelToken);

        /// <summary>
        /// Connects to the WebSocket server.
        /// </summary>
        /// <param name="host">The WebSocket server host</param>
        Task ConnectAsync(string host);
        /// <summary>
        /// Disconnects from the WebSocket server.
        /// </summary>
        /// <param name="closeCode">The WebSocket close code</param>
        Task DisconnectAsync(int closeCode = 1000);

        /// <summary>
        /// Sends a binary message to the WebSocket server.
        /// </summary>
        /// <param name="data">The binary data to send</param>
        /// <returns>A task that represents the asynchronous send operation</returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> data);
    }
}
