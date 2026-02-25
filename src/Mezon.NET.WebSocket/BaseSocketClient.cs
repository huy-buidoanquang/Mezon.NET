using System;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.Protobuf.Realtime;

namespace Mezon.NET.WebSocket
{
    public abstract partial class BaseSocketClient : Api.BaseMezonClient, IMezonClient, IApiClientProvider
    {
        /// <summary>
        ///     Gets the estimated round-trip latency, in milliseconds, to the gateway server.
        /// </summary>
        /// <returns>
        ///     An <see cref="int"/> that represents the round-trip latency to the WebSocket server. Please
        ///     note that this value does not represent a "true" latency for operations such as sending a message.
        /// </returns>
        public abstract long Latency { get; protected set; }

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

        public abstract Task ConnectAsync();

        public abstract Task DisconnectAsync();

        public Task Ping() => ApiClient.Ping();

        public Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic) => ApiClient.JoinChannelChat(clanId, channelId, channelType, isPublic);

        public Task JoinClanChat(long clanId) => ApiClient.JoinClanChat(clanId);

        IMezonClient IApiClientProvider.MezonApiClient => RestClient;
    }
}
