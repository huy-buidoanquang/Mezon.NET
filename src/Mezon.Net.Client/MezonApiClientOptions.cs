using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Transport;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Api
{

    /// <summary>
    ///     Represents a configuration class for <see cref="MezonApiClient"/>.
    /// </summary>
    public class MezonApiClientOptions : MezonOptions
    {
        public MezonApiClientOptions()
        {
        }

        public MezonApiClientOptions(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }

        /// <summary>
        /// Gets or sets the provider for creating REST clients. Defaults to <see cref="DefaultRestClientProvider.Instance"/>.
        /// </summary>
        public RestClientProvider RestClientProvider { get; set; } = DefaultRestClientProvider.Instance;

        /// <summary>
        /// Gets or sets the provider for creating Network Transporter. Defaults to <see cref="DefaultNetworkTransportProvider.Instance"/>.
        /// </summary>
        public MezonNetworkTransportProvider NetworkTransportProvider { get; set; } = DefaultNetworkTransportProvider.Instance;


        public string ApiBasePath { get; set; } = string.Empty;
    }
}
