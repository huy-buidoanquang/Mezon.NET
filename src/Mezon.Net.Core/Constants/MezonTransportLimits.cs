namespace Mezon.Net.Core
{
    /// <summary>
    ///     Default socket transport rate limits enforced client-side before sending over Abridged TCP / WebSocket.
    /// </summary>
    /// <remarks>
    ///     These limits mirror Mezon gateway guidance and are applied by the socket client's transport rate limiter.
    ///     Override per client via <c>MezonSocketClientOptions</c> when needed.
    /// </remarks>
    public static class MezonTransportLimits
    {
        /// <summary>
        ///     Maximum socket API requests allowed per second under normal operation.
        /// </summary>
        public const int MaxRequestsPerSecond = 60;

        /// <summary>
        ///     Maximum socket API requests allowed per minute under normal operation.
        /// </summary>
        public const int MaxRequestsPerMinute = 500;

        /// <summary>
        ///     Maximum socket requests allowed per second while the connection handshake is in progress.
        /// </summary>
        public const int MaxConnectRequestsPerSecond = 2;
    }
}
