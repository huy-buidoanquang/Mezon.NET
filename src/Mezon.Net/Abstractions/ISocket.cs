using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Socket;

namespace Mezon.NET.Abstractions
{
    /// <summary>
    /// A socket connection to the Mezon server.
    /// </summary>
    public interface ISocket : ISocketC2S, ISocketS2C
    {
        /// <summary>
        /// Gets a value indicating whether the connection is open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// An application-level heartbeat timeout.
        /// </summary>
        event Action OnHeartbeatTimeout;

        /// <summary>
        /// Connects to the server.
        /// </summary>
        Task<Session> ConnectAsync(Session session, bool createStatus, int? connectTimeoutMs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Close from the server.
        /// </summary>
        Task CloseAsync(CancellationToken cancellation = default);

        Task<object> SendAsync<T>(T message, int sendTimeout = MezonSocketOptions.DefaultConnectTimeoutMs, CancellationToken cancellationToken = default) where T : SocketSendBase;

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        Task DisconnectAsync(bool fireDisconnectEvent, CancellationToken cancellationToken = default);
    }
}
