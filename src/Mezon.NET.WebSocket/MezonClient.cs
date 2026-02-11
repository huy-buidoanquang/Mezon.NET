using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Logging;

namespace Mezon.NET.WebSocket
{
    public partial class MezonClient : BaseMezonClient, IApiClientProvider
    {
        public override Api.MezonClient RestClient { get; }

        public MezonClient() : this(new MezonSocketClientConfiguration())
        {
        }

        public MezonClient(MezonSocketClientConfiguration configuration) : base(configuration, CreateApiClient(configuration))
        {
            RestClient = new Api.MezonClient(configuration, ApiClient);
        }

        public MezonClient(MezonSocketClientConfiguration configuration, LogManager logManager) : base(configuration, CreateApiClient(configuration), logManager)
        {
            RestClient = new Api.MezonClient(configuration, ApiClient);
        }

        public MezonClient(MezonSocketClientConfiguration configuration, IMezonApiClient apiClient) : base(configuration, apiClient)
        {
            RestClient = new Api.MezonClient(configuration, ApiClient);
        }

        public MezonClient(MezonSocketClientConfiguration configuration, LogManager logManager, IMezonApiClient apiClient) : base(configuration, apiClient, logManager)
        {
            RestClient = new Api.MezonClient(configuration, ApiClient);
        }

        private static MezonSocketApiClient CreateApiClient(MezonSocketClientConfiguration configuration)
            => new MezonSocketApiClient(configuration.HttpClientProvider, configuration.GRPCClientProvider, configuration.WebSocketClientProvider, configuration);

        IMezonApiClient IApiClientProvider.MezonApiClient => ApiClient;

        public async Task ConnectAsync()
        {
            await ApiClient.ConnectAsync();
        }
    }
}
