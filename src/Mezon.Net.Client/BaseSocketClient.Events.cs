using System;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client
{
    public partial class BaseSocketClient
    {
        public event Func<Task> ClientReadyEvent
        {
            add { _clientReadyEvent.Add(value); }
            remove { _clientReadyEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _clientReadyEvent = new AsyncEvent<Func<Task>>();

        public event Func<Pong, Task> PongReceivedEvent
        {
            add { _pongReceivedEvent.Add(value); }
            remove { _pongReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Pong, Task>> _pongReceivedEvent = new AsyncEvent<Func<Pong, Task>>();

        public event Func<Channel, Task> ChannelReceivedEvent
        {
            add { _channelReceivedEvent.Add(value); }
            remove { _channelReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Channel, Task>> _channelReceivedEvent = new AsyncEvent<Func<Channel, Task>>();

        public event Func<ClanJoin, Task> ClanJoinedEvent
        {
            add { _clanJoinedEvent.Add(value); }
            remove { _clanJoinedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ClanJoin, Task>> _clanJoinedEvent = new AsyncEvent<Func<ClanJoin, Task>>();

        public event Func<ChannelJoin, Task> ChannelJoinedEvent
        {
            add { _channelJoinedEvent.Add(value); }
            remove { _channelJoinedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelJoin, Task>> _channelJoinedEvent = new AsyncEvent<Func<ChannelJoin, Task>>();

        public event Func<ChannelLeave, Task> ChannelLeftEvent
        {
            add { _channelLeftEvent.Add(value); }
            remove { _channelLeftEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelLeave, Task>> _channelLeftEvent = new AsyncEvent<Func<ChannelLeave, Task>>();

        public event Func<Internal.Api.ChannelMessage, Task> ChannelMessageReceivedEvent
        {
            add { _channelMessageReceivedEvent.Add(value); }
            remove { _channelMessageReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Internal.Api.ChannelMessage, Task>> _channelMessageReceivedEvent = new AsyncEvent<Func<Internal.Api.ChannelMessage, Task>>();

        public event Func<ChannelMessageAck, Task> ChannelMessageAckReceivedEvent
        {
            add { _channelMessageAckReceivedEvent.Add(value); }
            remove { _channelMessageAckReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelMessageAck, Task>> _channelMessageAckReceivedEvent = new AsyncEvent<Func<ChannelMessageAck, Task>>();

        public event Func<ChannelMessageSend, Task> ChannelMessageSentEvent
        {
            add { _channelMessageSentEvent.Add(value); }
            remove { _channelMessageSentEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelMessageSend, Task>> _channelMessageSentEvent = new AsyncEvent<Func<ChannelMessageSend, Task>>();

        public event Func<ChannelMessageUpdate, Task> ChannelMessageUpdatedEvent
        {
            add { _channelMessageUpdatedEvent.Add(value); }
            remove { _channelMessageUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelMessageUpdate, Task>> _channelMessageUpdatedEvent = new AsyncEvent<Func<ChannelMessageUpdate, Task>>();

        public event Func<ChannelMessageRemove, Task> ChannelMessageRemovedEvent
        {
            add { _channelMessageRemovedEvent.Add(value); }
            remove { _channelMessageRemovedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelMessageRemove, Task>> _channelMessageRemovedEvent = new AsyncEvent<Func<ChannelMessageRemove, Task>>();

        public event Func<ChannelPresenceEvent, Task> ChannelPresenceChangedEvent
        {
            add { _channelPresenceChangedEvent.Add(value); }
            remove { _channelPresenceChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelPresenceEvent, Task>> _channelPresenceChangedEvent = new AsyncEvent<Func<ChannelPresenceEvent, Task>>();

        public event Func<global::Mezon.Net.Internal.Realtime.Error, Task> ErrorReceivedEvent
        {
            add { _errorReceivedEvent.Add(value); }
            remove { _errorReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<global::Mezon.Net.Internal.Realtime.Error, Task>> _errorReceivedEvent = new AsyncEvent<Func<global::Mezon.Net.Internal.Realtime.Error, Task>>();

        public event Func<Notifications, Task> NotificationsReceivedEvent
        {
            add { _notificationsReceivedEvent.Add(value); }
            remove { _notificationsReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Notifications, Task>> _notificationsReceivedEvent = new AsyncEvent<Func<Notifications, Task>>();

        public event Func<Internal.Api.Rpc, Task> RpcReceivedEvent
        {
            add { _rpcReceivedEvent.Add(value); }
            remove { _rpcReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Internal.Api.Rpc, Task>> _rpcReceivedEvent = new AsyncEvent<Func<Internal.Api.Rpc, Task>>();

        public event Func<Status, Task> StatusReceivedEvent
        {
            add { _statusReceivedEvent.Add(value); }
            remove { _statusReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Status, Task>> _statusReceivedEvent = new AsyncEvent<Func<Status, Task>>();

        public event Func<StatusFollow, Task> StatusFollowedEvent
        {
            add { _statusFollowedEvent.Add(value); }
            remove { _statusFollowedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StatusFollow, Task>> _statusFollowedEvent = new AsyncEvent<Func<StatusFollow, Task>>();

        public event Func<StatusPresenceEvent, Task> StatusPresenceChangedEvent
        {
            add { _statusPresenceChangedEvent.Add(value); }
            remove { _statusPresenceChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StatusPresenceEvent, Task>> _statusPresenceChangedEvent = new AsyncEvent<Func<StatusPresenceEvent, Task>>();
        public event Func<StatusUnfollow, Task> StatusUnfollowedEvent
        {
            add { _statusUnfollowedEvent.Add(value); }
            remove { _statusUnfollowedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StatusUnfollow, Task>> _statusUnfollowedEvent = new AsyncEvent<Func<StatusUnfollow, Task>>();

        public event Func<StatusUpdate, Task> StatusUpdatedEvent
        {
            add { _statusUpdatedEvent.Add(value); }
            remove { _statusUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StatusUpdate, Task>> _statusUpdatedEvent = new AsyncEvent<Func<StatusUpdate, Task>>();

        public event Func<StreamData, Task> StreamDataReceivedEvent
        {
            add { _streamDataReceivedEvent.Add(value); }
            remove { _streamDataReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StreamData, Task>> _streamDataReceivedEvent = new AsyncEvent<Func<StreamData, Task>>();

        public event Func<StreamPresenceEvent, Task> StreamPresenceChangedEvent
        {
            add { _streamPresenceChangedEvent.Add(value); }
            remove { _streamPresenceChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<StreamPresenceEvent, Task>> _streamPresenceChangedEvent = new AsyncEvent<Func<StreamPresenceEvent, Task>>();
        public event Func<MessageTypingEvent, Task> MessageTypingReceivedEvent
        {
            add { _messageTypingReceivedEvent.Add(value); }
            remove { _messageTypingReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<MessageTypingEvent, Task>> _messageTypingReceivedEvent = new AsyncEvent<Func<MessageTypingEvent, Task>>();

        public event Func<LastSeenMessageEvent, Task> LastSeenMessageUpdatedEvent
        {
            add { _lastSeenMessageUpdatedEvent.Add(value); }
            remove { _lastSeenMessageUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<LastSeenMessageEvent, Task>> _lastSeenMessageUpdatedEvent = new AsyncEvent<Func<LastSeenMessageEvent, Task>>();

        public event Func<Internal.Api.MessageReaction, Task> MessageReactionReceivedEvent
        {
            add { _messageReactionReceivedEvent.Add(value); }
            remove { _messageReactionReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Internal.Api.MessageReaction, Task>> _messageReactionReceivedEvent = new AsyncEvent<Func<Internal.Api.MessageReaction, Task>>();

        public event Func<VoiceJoinedEvent, Task> VoiceJoinedEvent
        {
            add { _voiceJoinedEvent.Add(value); }
            remove { _voiceJoinedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<VoiceJoinedEvent, Task>> _voiceJoinedEvent = new AsyncEvent<Func<VoiceJoinedEvent, Task>>();

        public event Func<VoiceLeavedEvent, Task> VoiceLeavedEvent
        {
            add { _voiceLeavedEvent.Add(value); }
            remove { _voiceLeavedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<VoiceLeavedEvent, Task>> _voiceLeavedEvent = new AsyncEvent<Func<VoiceLeavedEvent, Task>>();

        public event Func<VoiceStartedEvent, Task> VoiceStartedEvent
        {
            add { _voiceStartedEvent.Add(value); }
            remove { _voiceStartedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<VoiceStartedEvent, Task>> _voiceStartedEvent = new AsyncEvent<Func<VoiceStartedEvent, Task>>();

        public event Func<VoiceEndedEvent, Task> VoiceEndedEvent
        {
            add { _voiceEndedEvent.Add(value); }
            remove { _voiceEndedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<VoiceEndedEvent, Task>> _voiceEndedEvent = new AsyncEvent<Func<VoiceEndedEvent, Task>>();

        public event Func<ChannelCreatedEvent, Task> ChannelCreatedEvent
        {
            add { _channelCreatedEvent.Add(value); }
            remove { _channelCreatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelCreatedEvent, Task>> _channelCreatedEvent = new AsyncEvent<Func<ChannelCreatedEvent, Task>>();

        public event Func<ChannelDeletedEvent, Task> ChannelDeletedEvent
        {
            add { _channelDeletedEvent.Add(value); }
            remove { _channelDeletedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelDeletedEvent, Task>> _channelDeletedEvent = new AsyncEvent<Func<ChannelDeletedEvent, Task>>();

        public event Func<ChannelUpdatedEvent, Task> ChannelUpdatedEvent
        {
            add { _channelUpdatedEvent.Add(value); }
            remove { _channelUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelUpdatedEvent, Task>> _channelUpdatedEvent = new AsyncEvent<Func<ChannelUpdatedEvent, Task>>();

        public event Func<LastPinMessageEvent, Task> LastPinMessageUpdatedEvent
        {
            add { _lastPinMessageUpdatedEvent.Add(value); }
            remove { _lastPinMessageUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<LastPinMessageEvent, Task>> _lastPinMessageUpdatedEvent = new AsyncEvent<Func<LastPinMessageEvent, Task>>();

        public event Func<CustomStatusEvent, Task> CustomStatusChangedEvent
        {
            add { _customStatusChangedEvent.Add(value); }
            remove { _customStatusChangedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<CustomStatusEvent, Task>> _customStatusChangedEvent = new AsyncEvent<Func<CustomStatusEvent, Task>>();

        public event Func<UserChannelAdded, Task> UserChannelAddedEvent
        {
            add { _userChannelAddedEvent.Add(value); }
            remove { _userChannelAddedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<UserChannelAdded, Task>> _userChannelAddedEvent = new AsyncEvent<Func<UserChannelAdded, Task>>();

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

        public event Func<Task> MessageButtonClickedEvent
        {
            add { _messageButtonClickedEvent.Add(value); }
            remove { _messageButtonClickedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _messageButtonClickedEvent = new AsyncEvent<Func<Task>>();

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

        public event Func<Task> DropdownBoxSelectedEvent
        {
            add { _dropdownBoxSelectedEvent.Add(value); }
            remove { _dropdownBoxSelectedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<Task>> _dropdownBoxSelectedEvent = new AsyncEvent<Func<Task>>();

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

        public event Func<ApiRequestEvent, Task> LocalCacheUpdatedEvent
        {
            add { _localCacheUpdatedEvent.Add(value); }
            remove { _localCacheUpdatedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ApiRequestEvent, Task>> _localCacheUpdatedEvent = new AsyncEvent<Func<ApiRequestEvent, Task>>();

        public event Func<ApiRequestEvent, Task> ApiRequestReceivedEvent
        {
            add { _apiRequestReceivedEvent.Add(value); }
            remove { _apiRequestReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ApiRequestEvent, Task>> _apiRequestReceivedEvent = new AsyncEvent<Func<ApiRequestEvent, Task>>();

        public event Func<ListChannelUsersBannedEvent, Task> ChannelUsersBannedListedEvent
        {
            add { _channelUsersBannedListedEvent.Add(value); }
            remove { _channelUsersBannedListedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ListChannelUsersBannedEvent, Task>> _channelUsersBannedListedEvent = new AsyncEvent<Func<ListChannelUsersBannedEvent, Task>>();

        public event Func<global::Mezon.Net.Internal.Api.Session, Task> SessionRefreshedEvent
        {
            add { _sessionRefreshedEvent.Add(value); }
            remove { _sessionRefreshedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<global::Mezon.Net.Internal.Api.Session, Task>> _sessionRefreshedEvent = new AsyncEvent<Func<global::Mezon.Net.Internal.Api.Session, Task>>();

        public event Func<ChannelArchiveEvent, Task> ChannelArchivedEvent
        {
            add { _channelArchivedEvent.Add(value); }
            remove { _channelArchivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<ChannelArchiveEvent, Task>> _channelArchivedEvent = new AsyncEvent<Func<ChannelArchiveEvent, Task>>();

        public event Func<TopicInMessageEvent, Task> TopicInMessageReceivedEvent
        {
            add { _topicInMessageReceivedEvent.Add(value); }
            remove { _topicInMessageReceivedEvent.Remove(value); }
        }
        internal readonly AsyncEvent<Func<TopicInMessageEvent, Task>> _topicInMessageReceivedEvent = new AsyncEvent<Func<TopicInMessageEvent, Task>>();

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
