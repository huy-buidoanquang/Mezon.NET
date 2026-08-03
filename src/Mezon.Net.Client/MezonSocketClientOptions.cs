using Mezon.Net.Core;

namespace Mezon.Net.Client
{
    /// <summary>
    ///     Represents a configuration class for <see cref="MezonSocketClient"/>.
    /// </summary>
    public class MezonSocketClientOptions : MezonApiClientOptions
    {
        /// <summary>
        ///     Default connection timeout in milliseconds.
        /// </summary>
        public const int DefaultConnectionTimeoutInMilliseconds = 30000;

        /// <summary>
        ///     Default event-handler timeout in milliseconds.
        /// </summary>
        public const int DefaultSocketHandlerTimeoutInMilliseconds = 3000;

        /// <summary>
        ///     Default heartbeat interval in milliseconds.
        /// </summary>
        public const int DefaultHeartbeatIntervalInMilliseconds = 10000;

        /// <summary>
        ///     Initializes a new <see cref="MezonSocketClientOptions"/> instance with default settings.
        /// </summary>
        public MezonSocketClientOptions()
        {
        }

        /// <summary>
        ///     Gets or sets the maximum socket API requests allowed per second (global transport limit).
        /// </summary>
        public int MaxTransportRequestsPerSecond { get; set; } = MezonTransportLimits.MaxRequestsPerSecond;

        /// <summary>
        ///     Gets or sets the maximum socket API requests allowed per minute (global transport limit).
        /// </summary>
        public int MaxTransportRequestsPerMinute { get; set; } = MezonTransportLimits.MaxRequestsPerMinute;

        /// <summary>
        ///     Gets or sets the maximum socket requests per second while the connection handshake is in progress.
        /// </summary>
        public int MaxConnectRequestsPerSecond { get; set; } = MezonTransportLimits.MaxConnectRequestsPerSecond;

        /// <summary>
        ///     Initializes a new <see cref="MezonSocketClientOptions"/> instance with the specified gateway endpoint.
        /// </summary>
        /// <param name="host">Gateway host name.</param>
        /// <param name="port">Gateway port.</param>
        /// <param name="useSSL">Whether to use TLS.</param>
        public MezonSocketClientOptions(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }

        /// <summary>
        ///     Gets or sets the time, in milliseconds, to wait for a connection to complete before aborting.
        /// </summary>
        public int ConnectionTimeoutInMilliseconds { get; set; } = DefaultConnectionTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the timeout for event handlers, in milliseconds, after which a warning will be logged.
        ///     Setting this property to <see langword="null"/> disables this check (default; preferred for hot paths).
        /// </summary>
        public int? SocketHandlerTimeoutInMilliseconds { get; set; } = DefaultSocketHandlerTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the interval, in milliseconds, for the heartbeat.
        /// </summary>
        public int HeartbeatIntervalInMilliseconds { get; set; } = DefaultHeartbeatIntervalInMilliseconds;

        /// <summary>
        ///     When <see langword="true"/>, the socket gateway URL includes <c>status=true</c> on connect (presence bootstrap).
        /// </summary>
        public bool CreateStatusOnConnect { get; set; } = true;
    }
}
