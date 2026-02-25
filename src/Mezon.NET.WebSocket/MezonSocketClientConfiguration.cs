using Mezon.NET.Api;
using Mezon.NET.Abstractions;
using Mezon.NET.WebSocket.Providers;

namespace Mezon.NET.WebSocket
{
    public class MezonSocketClientConfiguration : MezonApiClientConfiguration
    {
        public const int DefaultConnectionTimeoutInMilliseconds = 30000;
        public const int DefaultHandlerTimeoutInMilliseconds = 3000;

        public MezonSocketClientConfiguration()
        {
        }

        public MezonSocketClientConfiguration(string host, string port, bool useSSL) : base(host, port, useSSL)
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
        public int HeartbeatIntervalInMilliseconds { get; set; } = 30000;

        /// <summary> Gets or sets the provider used to generate new gRPC connections. </summary>
        public WebSocketClientProvider WebSocketClientProvider { get; set; } = DefaultWebSocketClientProvider.Instance;
    }
}
