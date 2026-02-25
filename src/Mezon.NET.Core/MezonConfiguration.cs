using System.Reflection;
using Mezon.NET.Logging;

namespace Mezon.NET.Core
{
    public class MezonConfiguration
    {
        public const int MaxTimeRetry = 10;
        public const int DefaultApiTimeoutInMilliseconds = 7000;
        public const int DefaultSocketTimeoutInMilliseconds = 3000;
        public const string DefaultMMNApi = "https://dong.mezon.ai/mmn-api/";
        public const string DefaultZKApi = "https://dong.mezon.ai/zk-api/";
        public const string DefaultServerKey = "defaultkey";
        public const string DefaultHost = "dev-mezon.nccsoft.vn";
        public const string DefaultPort = "8080";
        public const bool DefaultUseSSL = true;

        /// <summary>
        ///     Gets the Mezon.Net version, including the build number.
        /// </summary>
        /// <returns>
        ///     A string containing the detailed version information, including its build number; <c>Unknown</c> when
        ///     the version fails to be fetched.
        /// </returns>
        public static string Version { get; } =
            typeof(MezonConfiguration).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(MezonConfiguration).GetTypeInfo().Assembly.GetName().Version?.ToString(2) ??
            "Unknown";

        public string ServerKey { get; set; } = DefaultServerKey;

        public string Host { get; set; } = DefaultHost;

        public string Port { get; set; } = DefaultPort;

        public bool UseSSL { get; set; } = DefaultUseSSL;

        /// <summary>
        ///     Returns the base Gateway Api Url.
        /// </summary>
        /// <returns>
        ///     The base Mezon Gateway Api Url.
        /// </returns>
        public string GatewayBasePath => $"{(UseSSL ? "https" : "http")}://{Host}:{Port}";

        /// <summary>
        ///     Returns the base MMN Api Url.
        /// </summary>
        /// <returns>
        ///     The base Mezon Mainnet Api Url.
        /// </returns>
        public string MMNApiUrl { get; set; } = DefaultMMNApi;

        /// <summary>
        ///     Returns the base Zk Api Url.
        /// </summary>
        /// <returns>
        ///     The base Zero-Knowledge Api Url.
        /// </returns>
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
        ///     If set to <see langword="true" />, the library will attempt to print the current version of the library, as well as
        ///     the API version it uses on startup.
        /// </remarks>
        internal bool DisplayInitialLog { get; set; } = true;

        public int ApiTimeoutInMilliseconds { get; set; } = DefaultApiTimeoutInMilliseconds;

        public int SocketTimeoutInMilliseconds { get; set; } = DefaultSocketTimeoutInMilliseconds;

        public MezonConfiguration()
        {
        }

        public MezonConfiguration(string host, string port, bool useSSL)
        {
            Host = host;
            Port = port;
            UseSSL = useSSL;
        }
    }
}
