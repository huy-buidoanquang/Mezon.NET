using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.NET.Abstractions
{
    public delegate IWebSocketClient WebSocketClientProvider();
    public delegate ValueTask BinaryMessageReceivedHandler(ReadOnlyMemory<byte> data);

    public interface IWebSocketClient : IDisposable
    {
        /// <summary>
        /// Event raised when a binary message is received
        /// </summary>
        event BinaryMessageReceivedHandler BinaryMessageReceived;
        /// <summary>
        /// Event raised when the WebSocket connection is closed
        /// </summary>
        event Func<Exception, Task> Closed;
        /// <summary>
        /// Event raised when the WebSocket connection is opened
        /// </summary>
        event Func<Task> Opened;
        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event Func<Exception, Task> ErrorOccurred;

        void SetHeader(string key, string value);
        void SetCancelToken(CancellationToken cancelToken);
        Task ConnectAsync(string host);
        Task DisconnectAsync(int closeCode = 1000);
        //Task SendAsync(byte[] data, int index, int count, bool isText);
        ValueTask SendAsync(ReadOnlyMemory<byte> data);
    }
}
