using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Core.Abstractions
{
    public interface IMezonNetworkTransporter : IDisposable
    {
        public delegate IMezonNetworkTransporter MezonNetworkTransportProvider(TransportType transportType);

        /// <summary>
        /// Event raised when a binary message is received
        /// </summary>
        Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        /// <summary>
        /// Event raised when the network connection is opened
        /// </summary>
        Func<Task>? Opened { get; set; }
        /// <summary>
        /// Event raised when the network connection is closed
        /// </summary>
        Func<Exception?, Task>? Closed { get; set; }
        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        Func<Exception, Task>? ErrorOccurred { get; set; }

        /// <summary>
        /// Sets a header to be included in the network handshake request.
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value</param>
        /// <remarks>
        /// This method should be called before connecting to the network server.
        /// </remarks>
        void SetHeader(IDictionary<string, string> headers);
        /// <summary>
        /// Sets the cancellation token to be used for network operations.
        /// </summary>
        /// <param name="cancelToken">The cancellation token</param>
        /// <remarks>
        /// This method should be called before connecting to the network server.
        /// </remarks>
        void SetCancelToken(CancellationToken cancellationToken);
        /// <summary>
        /// Connects to the network server.
        /// </summary>
        /// <param name="host">The network server host</param>
        /// <param name="port">The network server port</param>
        /// <param name="createStatus">Flag to create status</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false);
        /// <summary>
        /// Disconnects from the network server.
        /// </summary>
        /// <param name="closeCode">The network close code</param>
        /// <param name="reason">The reason for the disconnection</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task DisconnectAsync(int closeCode = 1000, string? reason = null);
        /// <summary>
        /// Sends a binary message to the network server.
        /// </summary>
        /// <param name="data">The binary data to send</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task that represents the asynchronous send operation</returns>
        ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data);
    }
}
