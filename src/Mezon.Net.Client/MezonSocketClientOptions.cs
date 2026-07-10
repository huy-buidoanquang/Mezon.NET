using Mezon.Net.Core.Constants;

namespace Mezon.Net.Client
{
    /// <summary>
    ///     Represents a configuration class for <see cref="MezonSocketClient"/>.
    /// </summary>
    public class MezonSocketClientOptions : MezonApiClientOptions
    {
        public const int DefaultConnectionTimeoutInMilliseconds = 30000;
        public const int DefaultHandlerTimeoutInMilliseconds = 3000;
        public const int DefaultHeartbeatIntervalInMilliseconds = 10000;

        public MezonSocketClientOptions()
        {
        }

        /// <summary>
        ///     Maximum socket API requests allowed per second (global transport limit).
        /// </summary>
        public int MaxTransportRequestsPerSecond { get; set; } = MezonTransportLimits.MaxRequestsPerSecond;

        /// <summary>
        ///     Maximum socket API requests allowed per minute (global transport limit).
        /// </summary>
        public int MaxTransportRequestsPerMinute { get; set; } = MezonTransportLimits.MaxRequestsPerMinute;

        /// <summary>
        ///     Maximum socket requests per second while the connection handshake is in progress.
        /// </summary>
        public int MaxConnectRequestsPerSecond { get; set; } = MezonTransportLimits.MaxConnectRequestsPerSecond;

        public MezonSocketClientOptions(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }

        /// <summary>
        ///     Gets or sets the time, in milliseconds, to wait for a connection to complete before aborting.
        /// </summary>
        public int ConnectionTimeoutInMilliseconds { get; set; } = DefaultConnectionTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the timeout for event handlers, in milliseconds, after which a warning will be logged.
        ///     Setting this property to <see langword="null" />disables this check.
        /// </summary>
        public int SocketHandlerTimeoutInMilliseconds { get; set; } = DefaultHandlerTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the interval, in milliseconds, for the heartbeat.
        /// </summary>
        public int HeartbeatIntervalInMilliseconds { get; set; } = DefaultHeartbeatIntervalInMilliseconds;

        /// <summary>
        ///     When true, the socket gateway URL includes <c>status=true</c> on connect (presence bootstrap).
        /// </summary>
        public bool CreateStatusOnConnect { get; set; } = true;
    }
}
