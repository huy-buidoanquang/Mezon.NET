using System.Threading.Tasks;
using Mezon.NET.Abstractions;

namespace Mezon.NET.WebSocket
{
    public abstract partial class BaseSocketClient : Api.BaseMezonClient, IMezonClient, IApiClientProvider
    {
        protected new readonly MezonSocketClientConfiguration Configuration;

        internal new MezonSocketApiClient ApiClient => (base.ApiClient as MezonSocketApiClient)!;

        public abstract Api.MezonClient RestClient { get; }

        /// <summary>
        ///     Initializes a new <see cref="BaseSocketClient"/> with the provided configuration.
        /// </summary>
        /// <param name="configuration">The configuration to be used with the client.</param>
        internal BaseSocketClient(MezonSocketClientConfiguration configuration, IMezonApiClient apiClient) : base(configuration, apiClient)
        {
            Configuration = configuration;
        }

        public Task JoinClanChat(long clanId) => ApiClient.JoinClanChat(clanId);

        IMezonClient IApiClientProvider.MezonApiClient => RestClient;
    }
}
