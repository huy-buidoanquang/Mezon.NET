using Mezon.Net.Core;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Api
{

    /// <summary>
    ///     Represents a configuration class for <see cref="MezonApiClient"/>.
    /// </summary>
    public class MezonApiClientConfiguration : MezonConfiguration
    {
        public MezonApiClientConfiguration()
        {
        }

        public MezonApiClientConfiguration(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }

        /// <summary> Gets or sets the provider used to generate new HTTP connections. </summary>
        public RestClientProvider HttpClientProvider { get; set; } = DefaultRestClientProvider.Instance;

        /// <summary> Gets or sets the provider used to generate new gRPC connections. </summary>
        public GRPCClientProvider GRPCClientProvider { get; set; } = DefaultGRPCClientProvider.Instance;

        public string ApiBasePath { get; set; } = string.Empty;
    }
}
