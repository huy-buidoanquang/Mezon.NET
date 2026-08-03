using System;
using System.Reflection;
using System.Threading.Tasks;
using Mezon.Net.Logging;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Shared configuration for Mezon.Net clients (host, timeouts, logging, and defaults).
    /// </summary>
    public class MezonOptions
    {
        /// <summary>
        ///     Maximum number of automatic retries for transient failures.
        /// </summary>
        public const int MaxTimeRetry = 10;

        /// <summary>
        ///     Default REST/API send timeout in milliseconds.
        /// </summary>
        public const int DefaultApiTimeoutInMilliseconds = 7000;

        /// <summary>
        ///     Default socket send timeout in milliseconds.
        /// </summary>
        public const int DefaultSocketTimeoutInMilliseconds = 7000;

        /// <summary>
        ///     Default Mezon Mainnet (MMN) gRPC node endpoint. Empty disables automatic MMN initialization.
        /// </summary>
        public const string DefaultMMNApi = "https://dong.mezon.ai/mmn-api";

        /// <summary>
        ///     Default Zero-Knowledge (ZK) prove service base URL.
        /// </summary>
        public const string DefaultZKApi = "https://dong.mezon.ai/zk-api";

        /// <summary>
        ///     Default server key used for authentication bootstrap.
        /// </summary>
        public const string DefaultServerKey = "HTTP3m3zonPr0dkey";

        /// <summary>
        ///     Default Mezon gateway host.
        /// </summary>
        public const string DefaultHost = "gw.mezon.ai";

        /// <summary>
        ///     Default Mezon gateway port.
        /// </summary>
        public const string DefaultPort = "443";

        /// <summary>
        ///     Default value indicating whether TLS should be used.
        /// </summary>
        public const bool DefaultUseSSL = true;

        /// <summary>
        ///     Gets the Mezon.Net version, including the build number.
        /// </summary>
        /// <returns>
        ///     A string containing the detailed version information, including its build number; <c>Unknown</c> when
        ///     the version fails to be fetched.
        /// </returns>
        public static string Version { get; } =
            typeof(MezonOptions).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(MezonOptions).GetTypeInfo().Assembly.GetName().Version?.ToString(2) ??
            "Unknown";

        /// <summary>
        ///     Gets or sets the server key used for authentication bootstrap.
        /// </summary>
        public string ServerKey { get; set; } = DefaultServerKey;

        /// <summary>
        ///     Gets or sets the Mezon gateway host name.
        /// </summary>
        public string Host { get; set; } = DefaultHost;

        /// <summary>
        ///     Gets or sets the Mezon gateway port.
        /// </summary>
        public string Port { get; set; } = DefaultPort;

        /// <summary>
        ///     Gets or sets whether TLS should be used when connecting to the gateway.
        /// </summary>
        public bool UseSSL { get; set; } = DefaultUseSSL;

        /// <summary>
        ///     Returns the base Gateway Api Url.
        /// </summary>
        /// <returns>
        ///     The base Mezon Gateway Api Url.
        /// </returns>
        public string GatewayBasePath => $"{(UseSSL ? "https" : "http")}://{Host}:{Port}";

        /// <summary>
        ///     Gets or sets the MMN gRPC node endpoint. When empty, MMN initialization is skipped.
        /// </summary>
        public string MMNApiUrl { get; set; } = DefaultMMNApi;

        /// <summary>
        ///     Gets or sets the ZK prove service base URL (requests are sent to <c>/prove</c>).
        /// </summary>
        public string ZkApiUrl { get; set; } = DefaultZKApi;

        /// <summary>
        ///     Gets or sets the minimum log level severity that will be sent to the Log event.
        /// </summary>
        /// <returns>
        ///     The currently set <see cref="LogLevel"/> for logging level.
        /// </returns>
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        ///     Gets or sets whether the initial log entry should be printed.
        /// </summary>
        /// <remarks>
        ///     If set to <see langword="true"/>, the library will attempt to print the current version of the library, as well as
        ///     the API version it uses on startup.
        /// </remarks>
        internal bool DisplayInitialLog { get; set; } = true;

        /// <summary>
        ///     Gets or sets the default API send timeout in milliseconds.
        /// </summary>
        public int ApiTimeoutInMilliseconds { get; set; } = DefaultApiTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the default socket send timeout in milliseconds.
        /// </summary>
        public int SocketTimeoutInMilliseconds { get; set; } = DefaultSocketTimeoutInMilliseconds;

        /// <summary>
        ///     Gets or sets the preferred network transport.
        /// </summary>
        public TransportType TransportType { get; set; } = TransportType.Auto;

        /// <summary>
        ///     Gets or sets whether the client should automatically refresh an expired session.
        /// </summary>
        public bool AutoRefreshSession { get; set; } = true;

        /// <summary>
        ///     Gets or sets the default callback invoked when a request is delayed by a rate limiter.
        /// </summary>
        /// <remarks>
        ///     Per-request overrides can be supplied via <see cref="RequestOptions.RatelimitCallback"/>.
        ///     The callback is notified for client-side transport throttling (per-second, per-minute, and connect-phase limits).
        /// </remarks>
        public Func<IRateLimitInfo, Task>? DefaultRatelimitCallback { get; set; }

        /// <summary>
        ///     Initializes a new <see cref="MezonOptions"/> instance with default host settings.
        /// </summary>
        public MezonOptions()
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonOptions"/> instance with the specified gateway endpoint.
        /// </summary>
        /// <param name="host">Gateway host name.</param>
        /// <param name="port">Gateway port.</param>
        /// <param name="useSSL">Whether to use TLS.</param>
        public MezonOptions(string host, string port, bool useSSL)
        {
            Host = host;
            Port = port;
            UseSSL = useSSL;
        }
    }
}
