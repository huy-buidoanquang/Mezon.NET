using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Client.Models.Internal;
using Mezon.Net.Core;
using Mezon.Net.Models;

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

        /// <summary>
        ///     Current servers return a protobuf GenerateMeetTokenResponse, older ones a raw JWT; the socket client
        ///     accepts both. Hand-written so <c>generate_protobuf_boundary.py</c> cannot restore a plain
        ///     <c>ParseFrom</c> on this RPC.
        /// </summary>
        public async Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenParams body, RequestOptions? options = null)
        {
            var result = await ApiClient.GenerateMeetTokenAsync(
                GenerateMeetTokenParamsMapper.ToProto(body),
                includeZeroCurrentEndpointId: body.CurrentEndpointId == 0,
                options).ConfigureAwait(false);
            return new GenerateMeetTokenResponse(result);
        }

        public async Task<FriendListResponse> ListFriendsAsync(int? state, int? limit, string? cursor, bool? sortByLastSent, RequestOptions? options = null)
        {
            var result = await ApiClient.ListFriendsAsync(state, limit, cursor, sortByLastSent, options).ConfigureAwait(false);
            return new FriendListResponse(result);
        }

        public async Task<ChannelMessageListResponse> ListChannelMessagesAsync(long clanId, long channelId, long? messageId, int? direction, int? limit, long? topicId, long? lastSeenMessageId, RequestOptions? options = null)
        {
            var result = await ApiClient.ListChannelMessagesAsync(clanId, channelId, messageId, direction, limit, topicId, lastSeenMessageId, options).ConfigureAwait(false);
            return new ChannelMessageListResponse(result);
        }
    }
}
