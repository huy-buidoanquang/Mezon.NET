using System;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Client
{
    public abstract partial class BaseMezonSocketClient
    {
        public event Func<Task> ClientReadyEvent
        {
            add { _clientReadyEvent.Add(value); }
            remove { _clientReadyEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clientReadyEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserChannelRemovedEvent
        {
            add { _userChannelRemovedEvent.Add(value); }
            remove { _userChannelRemovedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userChannelRemovedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserClanRemovedEvent
        {
            add { _userClanRemovedEvent.Add(value); }
            remove { _userClanRemovedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userClanRemovedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanUpdatedEvent
        {
            add { _clanUpdatedEvent.Add(value); }
            remove { _clanUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanUpdatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanProfileUpdatedEvent
        {
            add { _clanProfileUpdatedEvent.Add(value); }
            remove { _clanProfileUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanProfileUpdatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> NameExistenceCheckedEvent
        {
            add { _nameExistenceCheckedEvent.Add(value); }
            remove { _nameExistenceCheckedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _nameExistenceCheckedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserProfileUpdatedEvent
        {
            add { _userProfileUpdatedEvent.Add(value); }
            remove { _userProfileUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userProfileUpdatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanUserAddedEvent
        {
            add { _clanUserAddedEvent.Add(value); }
            remove { _clanUserAddedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanUserAddedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanEventCreated
        {
            add { _clanEventCreated.Add(value); }
            remove { _clanEventCreated.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanEventCreated = new AsyncEvent<Func<Task>>();

        public event Func<Task> RoleAssignedEvent
        {
            add { _roleAssignedEvent.Add(value); }
            remove { _roleAssignedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _roleAssignedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanDeletedEvent
        {
            add { _clanDeletedEvent.Add(value); }
            remove { _clanDeletedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanDeletedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> CoffeeGivenEvent
        {
            add { _coffeeGivenEvent.Add(value); }
            remove { _coffeeGivenEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _coffeeGivenEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StickerCreatedEvent
        {
            add { _stickerCreatedEvent.Add(value); }
            remove { _stickerCreatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _stickerCreatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StickerUpdatedEvent
        {
            add { _stickerUpdatedEvent.Add(value); }
            remove { _stickerUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _stickerUpdatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StickerDeletedEvent
        {
            add { _stickerDeletedEvent.Add(value); }
            remove { _stickerDeletedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _stickerDeletedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> RoleChangedEvent
        {
            add { _roleChangedEvent.Add(value); }
            remove { _roleChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _roleChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> EmojiReceivedEvent
        {
            add { _emojiReceivedEvent.Add(value); }
            remove { _emojiReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _emojiReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StreamingJoinedEvent
        {
            add { _streamingJoinedEvent.Add(value); }
            remove { _streamingJoinedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _streamingJoinedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StreamingLeavedEvent
        {
            add { _streamingLeavedEvent.Add(value); }
            remove { _streamingLeavedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _streamingLeavedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StreamingStartedEvent
        {
            add { _streamingStartedEvent.Add(value); }
            remove { _streamingStartedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _streamingStartedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> StreamingEndedEvent
        {
            add { _streamingEndedEvent.Add(value); }
            remove { _streamingEndedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _streamingEndedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> PermissionsSetEvent
        {
            add { _permissionsSetEvent.Add(value); }
            remove { _permissionsSetEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _permissionsSetEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> PermissionChangedEvent
        {
            add { _permissionChangedEvent.Add(value); }
            remove { _permissionChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _permissionChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> TokenSentEvent
        {
            add { _tokenSentEvent.Add(value); }
            remove { _tokenSentEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _tokenSentEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserUnmutedEvent
        {
            add { _userUnmutedEvent.Add(value); }
            remove { _userUnmutedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userUnmutedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> WebrtcSignalingForwardedEvent
        {
            add { _webrtcSignalingForwardedEvent.Add(value); }
            remove { _webrtcSignalingForwardedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _webrtcSignalingForwardedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ActivityListedEvent
        {
            add { _activityListedEvent.Add(value); }
            remove { _activityListedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _activityListedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> IncomingCallPushedEvent
        {
            add { _incomingCallPushedEvent.Add(value); }
            remove { _incomingCallPushedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _incomingCallPushedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> SdTopicReceivedEvent
        {
            add { _sdTopicReceivedEvent.Add(value); }
            remove { _sdTopicReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _sdTopicReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> FollowReceivedEvent
        {
            add { _followReceivedEvent.Add(value); }
            remove { _followReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _followReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ChannelAppReceivedEvent
        {
            add { _channelAppReceivedEvent.Add(value); }
            remove { _channelAppReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _channelAppReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserStatusChangedEvent
        {
            add { _userStatusChangedEvent.Add(value); }
            remove { _userStatusChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userStatusChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> FriendRemovedEvent
        {
            add { _friendRemovedEvent.Add(value); }
            remove { _friendRemovedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _friendRemovedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> WebhookReceivedEvent
        {
            add { _webhookReceivedEvent.Add(value); }
            remove { _webhookReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _webhookReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> NotiUserChannelReceivedEvent
        {
            add { _notiUserChannelReceivedEvent.Add(value); }
            remove { _notiUserChannelReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _notiUserChannelReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ChannelAppDataJoinedEvent
        {
            add { _channelAppDataJoinedEvent.Add(value); }
            remove { _channelAppDataJoinedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _channelAppDataJoinedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> CanvasReceivedEvent
        {
            add { _canvasReceivedEvent.Add(value); }
            remove { _canvasReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _canvasReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> MessageUnpinnedEvent
        {
            add { _messageUnpinnedEvent.Add(value); }
            remove { _messageUnpinnedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _messageUnpinnedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> CategoryChangedEvent
        {
            add { _categoryChangedEvent.Add(value); }
            remove { _categoryChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _categoryChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ParticipantMeetStateChangedEvent
        {
            add { _participantMeetStateChangedEvent.Add(value); }
            remove { _participantMeetStateChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _participantMeetStateChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> AccountDeletedEvent
        {
            add { _accountDeletedEvent.Add(value); }
            remove { _accountDeletedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _accountDeletedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> EphemeralMessageSentEvent
        {
            add { _ephemeralMessageSentEvent.Add(value); }
            remove { _ephemeralMessageSentEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _ephemeralMessageSentEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> FriendBlockedEvent
        {
            add { _friendBlockedEvent.Add(value); }
            remove { _friendBlockedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _friendBlockedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> VoiceReactionSentEvent
        {
            add { _voiceReactionSentEvent.Add(value); }
            remove { _voiceReactionSentEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _voiceReactionSentEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> MarkedAsReadEvent
        {
            add { _markedAsReadEvent.Add(value); }
            remove { _markedAsReadEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _markedAsReadEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> DataSocketListedEvent
        {
            add { _dataSocketListedEvent.Add(value); }
            remove { _dataSocketListedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _dataSocketListedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> QuickMenuReceivedEvent
        {
            add { _quickMenuReceivedEvent.Add(value); }
            remove { _quickMenuReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _quickMenuReceivedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> FriendUnblockedEvent
        {
            add { _friendUnblockedEvent.Add(value); }
            remove { _friendUnblockedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _friendUnblockedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> MeetParticipantChangedEvent
        {
            add { _meetParticipantChangedEvent.Add(value); }
            remove { _meetParticipantChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _meetParticipantChangedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> OwnershipTransferredEvent
        {
            add { _ownershipTransferredEvent.Add(value); }
            remove { _ownershipTransferredEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _ownershipTransferredEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> FriendAddedEvent
        {
            add { _friendAddedEvent.Add(value); }
            remove { _friendAddedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _friendAddedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> UserBannedEvent
        {
            add { _userBannedEvent.Add(value); }
            remove { _userBannedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _userBannedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ArchivedThreadActivatedEvent
        {
            add { _archivedThreadActivatedEvent.Add(value); }
            remove { _archivedThreadActivatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _archivedThreadActivatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> AnonymousAllowedEvent
        {
            add { _anonymousAllowedEvent.Add(value); }
            remove { _anonymousAllowedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _anonymousAllowedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> ClanCreatedEvent
        {
            add { _clanCreatedEvent.Add(value); }
            remove { _clanCreatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clanCreatedEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> AIAgentEnabledEvent
        {
            add { _aIAgentEnabledEvent.Add(value); }
            remove { _aIAgentEnabledEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _aIAgentEnabledEvent = new AsyncEvent<Func<Task>>();
    }
}
