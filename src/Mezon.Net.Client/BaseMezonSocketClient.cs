using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Client
{
    public abstract partial class BaseMezonSocketClient : BaseMezonClient, IMezonClient, IApiClientProvider
    {
        /// <summary>
        ///     Gets the estimated round-trip latency, in milliseconds, to the gateway server.
        /// </summary>
        /// <returns>
        ///     An <see cref="int"/> that represents the round-trip latency to the WebSocket server. Please
        ///     note that this value does not represent a "true" latency for operations such as sending a message.
        /// </returns>
        public abstract long Latency { get; protected set; }

        protected new readonly MezonSocketClientOptions Options;

        internal new MezonSocketClient ApiClient => (base.ApiClient as MezonSocketClient)!;

        /// <summary>
        ///     Initializes a new <see cref="BaseMezonSocketClient"/> with the provided configuration.
        /// </summary>
        /// <param name="options">The configuration to be used with the client.</param>
        internal BaseMezonSocketClient(MezonSocketClientOptions options, IMezonApiClient apiClient) : base(options, apiClient)
        {
            Options = options;
        }

        public abstract Task ConnectAsync();

        public abstract Task DisconnectAsync();

        IMezonClient IApiClientProvider.MezonApiClient => this;
    }
}
