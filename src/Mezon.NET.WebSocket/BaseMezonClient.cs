using System.Threading.Tasks;
using Mezon.NET.Api;
using Mezon.NET.Abstractions;
using Mezon.NET.Logging;

namespace Mezon.NET.WebSocket
{
    public abstract partial class BaseMezonClient : Api.BaseMezonClient, IMezonClient
    {
        protected readonly MezonSocketClientConfiguration SocketClientConfiguration;

        internal new MezonSocketApiClient ApiClient => (base.ApiClient as MezonSocketApiClient)!;

        public abstract Api.MezonClient RestClient { get; }

        /// <summary>
        ///     Initializes a new <see cref="BaseMezonClient"/> with the provided configuration.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        internal BaseMezonClient(MezonSocketClientConfiguration mezonConfiguration, IMezonApiClient apiClient) : base(mezonConfiguration, apiClient)
        {
            SocketClientConfiguration = mezonConfiguration;
        }

        /// <summary>
        ///     Initializes a new <see cref="BaseMezonClient"/> with the provided configuration, API client, and external LogManager.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        /// <param name="apiClient">The API client to use for requests.</param>
        /// <param name="logManager">An external LogManager instance for centralized logging.</param>
        internal BaseMezonClient(MezonSocketClientConfiguration mezonConfiguration, IMezonApiClient apiClient, LogManager logManager) : base(mezonConfiguration, apiClient, logManager)
        {
            SocketClientConfiguration = mezonConfiguration;
        }

        public Task JoinClanChat(long clanId) => ApiClient.JoinClanChat(clanId);
    }
}
