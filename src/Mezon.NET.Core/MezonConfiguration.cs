using System.Reflection;
using Mezon.NET.Logging;

namespace Mezon.NET.Core
{
    public class MezonConfiguration
    {
        public const int MaxTimeRetry = 10;
        public const int DefaultTimeoutInMilliseconds = 7000;
        public const string DefaultMMNApi = "https://dong.mezon.ai/mmn-api/";
        public const string DefaultZKApi = "https://dong.mezon.ai/zk-api/";
        public const string DefaultServerKey = "defaultkey";

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

        public string Host { get; set; }

        public string Port { get; set; }

        public bool UseSSL { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

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
        ///     The currently set <see cref="LogSeverity"/> for logging level.
        /// </returns>
        public LogSeverity LogLevel { get; set; } = LogSeverity.Info;

        /// <summary>
        ///     Gets or sets whether the initial log entry should be printed.
        /// </summary>
        /// <remarks>
        ///     If set to <see langword="true" />, the library will attempt to print the current version of the library, as well as
        ///     the API version it uses on startup.
        /// </remarks>
        internal bool DisplayInitialLog { get; set; } = true;

        public int TimeoutInMilliseconds { get; set; } = DefaultTimeoutInMilliseconds;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MezonConfiguration(string host, string port, bool useSSL)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            Host = host;
            Port = port;
            UseSSL = useSSL;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MezonConfiguration()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }
    }
}
