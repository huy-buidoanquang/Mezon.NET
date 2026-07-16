using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Transport;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Client
{
    /// <summary>
    ///     Represents a configuration class for <see cref="MezonApiClient"/>.
    /// </summary>
    public class MezonApiClientOptions : MezonOptions
    {
        /// <summary>
        ///     Initializes a new <see cref="MezonApiClientOptions"/> instance with default settings.
        /// </summary>
        public MezonApiClientOptions()
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonApiClientOptions"/> instance with the specified gateway endpoint.
        /// </summary>
        /// <param name="host">Gateway host name.</param>
        /// <param name="port">Gateway port.</param>
        /// <param name="useSSL">Whether to use TLS.</param>
        public MezonApiClientOptions(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }

        /// <summary>
        ///     Gets or sets the provider for creating REST clients. Defaults to <see cref="DefaultRestClientProvider.Instance"/>.
        /// </summary>
        public RestClientProvider RestClientProvider { get; set; } = DefaultRestClientProvider.Instance;

        /// <summary>
        ///     Gets or sets the provider for creating network transporters. Defaults to <see cref="DefaultNetworkTransportProvider.Instance"/>.
        /// </summary>
        public MezonNetworkTransportProvider NetworkTransportProvider { get; set; } = DefaultNetworkTransportProvider.Instance;
    }
}
