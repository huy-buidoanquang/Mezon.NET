using System;
using System.Threading.Tasks;
using Mezon.Net.SDK.Abstractions;

namespace Mezon.Net.SDK
{
    internal sealed class MezonClient : IMezonClient
    {
        public string Token => throw new NotImplementedException();

        public string ClientId => throw new NotImplementedException();

        public string Host => throw new NotImplementedException();

        public bool UseSSL => throw new NotImplementedException();

        public string Port => throw new NotImplementedException();

        public string LoginBasePath => throw new NotImplementedException();

        public string MmnApiUrl => throw new NotImplementedException();

        public string ZkApiUrl => throw new NotImplementedException();

        public string AddressMMN => throw new NotImplementedException();

        public ICacheManager<string, IClan> Clans => throw new NotImplementedException();

        public ICacheManager<string, ITextChannel> Channels => throw new NotImplementedException();

        public event EventHandler Ready;

        public Task<object> AcceptFriendAsync(string userId, string username)
        {
            throw new NotImplementedException();
        }

        public Task<object> AddFriendAsync(string username)
        {
            throw new NotImplementedException();
        }

        public Task<object> AddQuickMenuAccessAsync(ApiQuickMenuAccessPayload payload)
        {
            throw new NotImplementedException();
        }

        public void CloseSocket()
        {
            throw new NotImplementedException();
        }

        public Task<object> CreateDMChannelAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<object> DeleteQuickMenuAccessAsync(string botId = null)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetAddressAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetCurrentNonceAsync(string userId, string tag = "pending")
        {
            throw new NotImplementedException();
        }

        public Task<object> GetEphemeralKeyPairAsync()
        {
            throw new NotImplementedException();
        }

        public Task<object> GetListFriendsAsync(int? limit = null, string state = null, string cursor = null)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetZkProofsAsync(ApiGetZkProofRequest request)
        {
            throw new NotImplementedException();
        }

        public void InitManager(string basePath, object sessionApi = null)
        {
            throw new NotImplementedException();
        }

        public Task<string> LoginAsync()
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnAddClanUser(Action<AddClanUserEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnChannelCreated(Action<ChannelCreatedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnChannelDeleted(Action<ChannelDeletedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnChannelMessage(Action<Abstractions.ChannelMessage> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnChannelUpdated(Action<ChannelUpdatedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnClanEventCreated(Action<CreateEventRequest> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnDropdownBoxSelected(Action<DropdownBoxSelected> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnGiveCoffee(Action<GiveCoffeeEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnMessageButtonClicked(Action<MessageButtonClicked> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnMessageReaction(Action<object> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnNotification(Action<Notifications> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnQuickMenuEvent(Action<object> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnRoleAssign(Action<RoleAssignedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnRoleEvent(Action<RoleEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnStreamingJoinedEvent(Action<StreamingJoinedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnStreamingLeavedEvent(Action<StreamingLeavedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnTokenSend(Action<TokenSentEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnUserChannelAdded(Action<UserChannelAddedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnUserChannelRemoved(Action<UserChannelRemoved> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnUserClanRemoved(Action<UserClanRemovedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnVoiceEndedEvent(Action<VoiceEndedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnVoiceJoinedEvent(Action<VoiceJoinedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnVoiceLeavedEvent(Action<VoiceLeavedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnVoiceStartedEvent(Action<VoiceStartedEvent> handler)
        {
            throw new NotImplementedException();
        }

        public IMezonClient OnWebrtcSignalingFwd(Action<WebrtcSignalingFwd> handler)
        {
            throw new NotImplementedException();
        }

        public Task<object> SendTokenAsync(ApiSentTokenRequest tokenEvent)
        {
            throw new NotImplementedException();
        }
    }
}
