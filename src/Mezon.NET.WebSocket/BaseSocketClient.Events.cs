//using System;
//using System.Threading.Tasks;
//using Mezon.Net.Core;
//using Mezon.Net.Internal.Protos;

//namespace Mezon.Net.WebSocket
//{
//    public partial class BaseSocketClient
//    {
//        public event Func<Task> ReadyEvent
//        {
//            add { _readyEvent.Add(value); }
//            remove { _readyEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _readyEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Pong, Task> PongEvent
//        {
//            add { _pongEvent.Add(value); }
//            remove { _pongEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Pong, Task>> _pongEvent = new AsyncEvent<Func<Pong, Task>>();

//        public event Func<Channel, Task> ChannelEvent
//        {
//            add { _channelEvent.Add(value); }
//            remove { _channelEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Channel, Task>> _channelEvent = new AsyncEvent<Func<Channel, Task>>();

//        public event Func<ClanJoin, Task> ClanJoinEvent
//        {
//            add { _clanJoinEvent.Add(value); }
//            remove { _clanJoinEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ClanJoin, Task>> _clanJoinEvent = new AsyncEvent<Func<ClanJoin, Task>>();

//        public event Func<ChannelJoin, Task> ChannelJoinEvent
//        {
//            add { _channelJoinEvent.Add(value); }
//            remove { _channelJoinEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelJoin, Task>> _channelJoinEvent = new AsyncEvent<Func<ChannelJoin, Task>>();

//        public event Func<ChannelLeave, Task> ChannelLeaveEvent
//        {
//            add { _channelLeaveEvent.Add(value); }
//            remove { _channelLeaveEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelLeave, Task>> _channelLeaveEvent = new AsyncEvent<Func<ChannelLeave, Task>>();

//        public event Func<ChannelMessage, Task> ChannelMessageEvent
//        {
//            add { _channelMessageEvent.Add(value); }
//            remove { _channelMessageEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelMessage, Task>> _channelMessageEvent = new AsyncEvent<Func<ChannelMessage, Task>>();

//        public event Func<ChannelMessageAck, Task> ChannelMessageAckEvent
//        {
//            add { _channelMessageAckEvent.Add(value); }
//            remove { _channelMessageAckEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelMessageAck, Task>> _channelMessageAckEvent = new AsyncEvent<Func<ChannelMessageAck, Task>>();

//        public event Func<ChannelMessageSend, Task> ChannelMessageSendEvent
//        {
//            add { _channelMessageSendEvent.Add(value); }
//            remove { _channelMessageSendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelMessageSend, Task>> _channelMessageSendEvent = new AsyncEvent<Func<ChannelMessageSend, Task>>();

//        public event Func<ChannelMessageUpdate, Task> ChannelMessageUpdateEvent
//        {
//            add { _channelMessageUpdateEvent.Add(value); }
//            remove { _channelMessageUpdateEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelMessageUpdate, Task>> _channelMessageUpdateEvent = new AsyncEvent<Func<ChannelMessageUpdate, Task>>();

//        public event Func<ChannelMessageRemove, Task> ChannelMessageRemoveEvent
//        {
//            add { _channelMessageRemoveEvent.Add(value); }
//            remove { _channelMessageRemoveEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelMessageRemove, Task>> _channelMessageRemoveEvent = new AsyncEvent<Func<ChannelMessageRemove, Task>>();

//        public event Func<ChannelPresenceEvent, Task> ChannelPresenceEvent
//        {
//            add { _channelPresenceEvent.Add(value); }
//            remove { _channelPresenceEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelPresenceEvent, Task>> _channelPresenceEvent = new AsyncEvent<Func<ChannelPresenceEvent, Task>>();

//        public event Func<Error, Task> ErrorEvent
//        {
//            add { _errorEvent.Add(value); }
//            remove { _errorEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Error, Task>> _errorEvent = new AsyncEvent<Func<Error, Task>>();

//        public event Func<Notifications, Task> NotificationsEvent
//        {
//            add { _notificationsEvent.Add(value); }
//            remove { _notificationsEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Notifications, Task>> _notificationsEvent = new AsyncEvent<Func<Notifications, Task>>();

//        public event Func<Rpc, Task> RpcEvent
//        {
//            add { _rpcEvent.Add(value); }
//            remove { _rpcEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Rpc, Task>> _rpcEvent = new AsyncEvent<Func<Rpc, Task>>();

//        public event Func<Status, Task> StatusEvent
//        {
//            add { _statusEvent.Add(value); }
//            remove { _statusEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Status, Task>> _statusEvent = new AsyncEvent<Func<Status, Task>>();

//        public event Func<StatusFollow, Task> StatusFollowEvent
//        {
//            add { _statusFollowEvent.Add(value); }
//            remove { _statusFollowEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StatusFollow, Task>> _statusFollowEvent = new AsyncEvent<Func<StatusFollow, Task>>();

//        public event Func<StatusPresenceEvent, Task> StatusPresenceEvent
//        {
//            add { _statusPresenceEvent.Add(value); }
//            remove { _statusPresenceEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StatusPresenceEvent, Task>> _statusPresenceEvent = new AsyncEvent<Func<StatusPresenceEvent, Task>>();
//        public event Func<StatusUnfollow, Task> StatusUnfollowEvent
//        {
//            add { _statusUnfollowEvent.Add(value); }
//            remove { _statusUnfollowEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StatusUnfollow, Task>> _statusUnfollowEvent = new AsyncEvent<Func<StatusUnfollow, Task>>();

//        public event Func<StatusUpdate, Task> StatusUpdateEvent
//        {
//            add { _statusUpdateEvent.Add(value); }
//            remove { _statusUpdateEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StatusUpdate, Task>> _statusUpdateEvent = new AsyncEvent<Func<StatusUpdate, Task>>();

//        public event Func<StreamData, Task> StreamDataEvent
//        {
//            add { _streamDataEvent.Add(value); }
//            remove { _streamDataEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StreamData, Task>> _streamDataEvent = new AsyncEvent<Func<StreamData, Task>>();

//        public event Func<StreamPresenceEvent, Task> StreamPresenceEvent
//        {
//            add { _streamPresenceEvent.Add(value); }
//            remove { _streamPresenceEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<StreamPresenceEvent, Task>> _streamPresenceEvent = new AsyncEvent<Func<StreamPresenceEvent, Task>>();
//        public event Func<MessageTypingEvent, Task> MessageTypingEvent
//        {
//            add { _messageTypingEvent.Add(value); }
//            remove { _messageTypingEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<MessageTypingEvent, Task>> _messageTypingEvent = new AsyncEvent<Func<MessageTypingEvent, Task>>();

//        public event Func<LastSeenMessageEvent, Task> LastSeenMessageEvent
//        {
//            add { _lastSeenMessageEvent.Add(value); }
//            remove { _lastSeenMessageEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<LastSeenMessageEvent, Task>> _lastSeenMessageEvent = new AsyncEvent<Func<LastSeenMessageEvent, Task>>();

//        public event Func<MessageReaction, Task> MessageReactionEvent
//        {
//            add { _messageReactionEvent.Add(value); }
//            remove { _messageReactionEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<MessageReaction, Task>> _messageReactionEvent = new AsyncEvent<Func<MessageReaction, Task>>();

//        public event Func<VoiceJoinedEvent, Task> VoiceJoinedEvent
//        {
//            add { _voiceJoinedEvent.Add(value); }
//            remove { _voiceJoinedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<VoiceJoinedEvent, Task>> _voiceJoinedEvent = new AsyncEvent<Func<VoiceJoinedEvent, Task>>();

//        public event Func<VoiceLeavedEvent, Task> VoiceLeavedEvent
//        {
//            add { _voiceLeavedEvent.Add(value); }
//            remove { _voiceLeavedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<VoiceLeavedEvent, Task>> _voiceLeavedEvent = new AsyncEvent<Func<VoiceLeavedEvent, Task>>();

//        public event Func<VoiceStartedEvent, Task> VoiceStartedEvent
//        {
//            add { _voiceStartedEvent.Add(value); }
//            remove { _voiceStartedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<VoiceStartedEvent, Task>> _voiceStartedEvent = new AsyncEvent<Func<VoiceStartedEvent, Task>>();

//        public event Func<VoiceEndedEvent, Task> VoiceEndedEvent
//        {
//            add { _voiceEndedEvent.Add(value); }
//            remove { _voiceEndedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<VoiceEndedEvent, Task>> _voiceEndedEvent = new AsyncEvent<Func<VoiceEndedEvent, Task>>();

//        public event Func<ChannelCreatedEvent, Task> ChannelCreatedEvent
//        {
//            add { _channelCreatedEvent.Add(value); }
//            remove { _channelCreatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelCreatedEvent, Task>> _channelCreatedEvent = new AsyncEvent<Func<ChannelCreatedEvent, Task>>();

//        public event Func<ChannelDeletedEvent, Task> ChannelDeletedEvent
//        {
//            add { _channelDeletedEvent.Add(value); }
//            remove { _channelDeletedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelDeletedEvent, Task>> _channelDeletedEvent = new AsyncEvent<Func<ChannelDeletedEvent, Task>>();

//        public event Func<ChannelUpdatedEvent, Task> ChannelUpdatedEvent
//        {
//            add { _channelUpdatedEvent.Add(value); }
//            remove { _channelUpdatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<ChannelUpdatedEvent, Task>> _channelUpdatedEvent = new AsyncEvent<Func<ChannelUpdatedEvent, Task>>();

//        public event Func<LastPinMessageEvent, Task> LastPinMessageEvent
//        {
//            add { _lastPinMessageEvent.Add(value); }
//            remove { _lastPinMessageEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<LastPinMessageEvent, Task>> _lastPinMessageEvent = new AsyncEvent<Func<LastPinMessageEvent, Task>>();

//        public event Func<CustomStatusEvent, Task> CustomStatusEvent
//        {
//            add { _customStatusEvent.Add(value); }
//            remove { _customStatusEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<CustomStatusEvent, Task>> _customStatusEvent = new AsyncEvent<Func<CustomStatusEvent, Task>>();

//        public event Func<UserChannelAdded, Task> UserChannelAddedEvent
//        {
//            add { _userChannelAddedEvent.Add(value); }
//            remove { _userChannelAddedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<UserChannelAdded, Task>> _userChannelAddedEvent = new AsyncEvent<Func<UserChannelAdded, Task>>();

//        public event Func<Task> UserChannelRemovedEvent
//        {
//            add { _userChannelRemovedEvent.Add(value); }
//            remove { _userChannelRemovedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _userChannelRemovedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UserClanRemovedEvent
//        {
//            add { _userClanRemovedEvent.Add(value); }
//            remove { _userClanRemovedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _userClanRemovedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ClanUpdatedEvent
//        {
//            add { _clanUpdatedEvent.Add(value); }
//            remove { _clanUpdatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _clanUpdatedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ClanProfileUpdatedEvent
//        {
//            add { _clanProfileUpdatedEvent.Add(value); }
//            remove { _clanProfileUpdatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _clanProfileUpdatedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> CheckNameExistedEvent
//        {
//            add { _checkNameExistedEvent.Add(value); }
//            remove { _checkNameExistedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _checkNameExistedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UserProfileUpdatedEvent
//        {
//            add { _userProfileUpdatedEvent.Add(value); }
//            remove { _userProfileUpdatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _userProfileUpdatedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> AddClanUserEvent
//        {
//            add { _addClanUserEvent.Add(value); }
//            remove { _addClanUserEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _addClanUserEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ClanEventCreated
//        {
//            add { _clanEventCreated.Add(value); }
//            remove { _clanEventCreated.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _clanEventCreated = new AsyncEvent<Func<Task>>();

//        public event Func<Task> RoleAssignEvent
//        {
//            add { _roleAssignEvent.Add(value); }
//            remove { _roleAssignEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _roleAssignEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ClanDeletedEvent
//        {
//            add { _clanDeletedEvent.Add(value); }
//            remove { _clanDeletedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _clanDeletedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> GiveCoffeeEvent
//        {
//            add { _giveCoffeeEvent.Add(value); }
//            remove { _giveCoffeeEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _giveCoffeeEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StickerCreateEvent
//        {
//            add { _stickerCreateEvent.Add(value); }
//            remove { _stickerCreateEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _stickerCreateEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StickerUpdateEvent
//        {
//            add { _stickerUpdateEvent.Add(value); }
//            remove { _stickerUpdateEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _stickerUpdateEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StickerDeleteEvent
//        {
//            add { _stickerDeleteEvent.Add(value); }
//            remove { _stickerDeleteEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _stickerDeleteEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> RoleEvent
//        {
//            add { _roleEvent.Add(value); }
//            remove { _roleEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _roleEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> EventEmoji
//        {
//            add { _eventEmoji.Add(value); }
//            remove { _eventEmoji.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _eventEmoji = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StreamingJoinedEvent
//        {
//            add { _streamingJoinedEvent.Add(value); }
//            remove { _streamingJoinedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _streamingJoinedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StreamingLeavedEvent
//        {
//            add { _streamingLeavedEvent.Add(value); }
//            remove { _streamingLeavedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _streamingLeavedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StreamingStartedEvent
//        {
//            add { _streamingStartedEvent.Add(value); }
//            remove { _streamingStartedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _streamingStartedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> StreamingEndedEvent
//        {
//            add { _streamingEndedEvent.Add(value); }
//            remove { _streamingEndedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _streamingEndedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> PermissionSetEvent
//        {
//            add { _permissionSetEvent.Add(value); }
//            remove { _permissionSetEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _permissionSetEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> PermissionChangedEvent
//        {
//            add { _permissionChangedEvent.Add(value); }
//            remove { _permissionChangedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _permissionChangedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> TokenSentEvent
//        {
//            add { _tokenSentEvent.Add(value); }
//            remove { _tokenSentEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _tokenSentEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> MessageButtonClickedEvent
//        {
//            add { _messageButtonClickedEvent.Add(value); }
//            remove { _messageButtonClickedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _messageButtonClickedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UnmuteEvent
//        {
//            add { _unmuteEvent.Add(value); }
//            remove { _unmuteEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _unmuteEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> WebrtcSignalingFwdEvent
//        {
//            add { _webrtcSignalingFwdEvent.Add(value); }
//            remove { _webrtcSignalingFwdEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _webrtcSignalingFwdEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ListActivityEvent
//        {
//            add { _listActivityEvent.Add(value); }
//            remove { _listActivityEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _listActivityEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> DropdownBoxSelectedEvent
//        {
//            add { _dropdownBoxSelectedEvent.Add(value); }
//            remove { _dropdownBoxSelectedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _dropdownBoxSelectedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> IncomingCallPushEvent
//        {
//            add { _incomingCallPushEvent.Add(value); }
//            remove { _incomingCallPushEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _incomingCallPushEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> SdTopicEvent
//        {
//            add { _sdTopicEvent.Add(value); }
//            remove { _sdTopicEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _sdTopicEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> FollowEvent
//        {
//            add { _followEvent.Add(value); }
//            remove { _followEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _followEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ChannelAppEvent
//        {
//            add { _channelAppEvent.Add(value); }
//            remove { _channelAppEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _channelAppEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UserStatusEvent
//        {
//            add { _userStatusEvent.Add(value); }
//            remove { _userStatusEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _userStatusEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> RemoveFriendEvent
//        {
//            add { _removeFriendEvent.Add(value); }
//            remove { _removeFriendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _removeFriendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> WebhookEvent
//        {
//            add { _webhookEvent.Add(value); }
//            remove { _webhookEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _webhookEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> NotiUserChannelEvent
//        {
//            add { _notiUserChannelEvent.Add(value); }
//            remove { _notiUserChannelEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _notiUserChannelEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> JoinChannelAppDataEvent
//        {
//            add { _joinChannelAppDataEvent.Add(value); }
//            remove { _joinChannelAppDataEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _joinChannelAppDataEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> CanvasEvent
//        {
//            add { _canvasEvent.Add(value); }
//            remove { _canvasEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _canvasEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UnpinMessageEvent
//        {
//            add { _unpinMessageEvent.Add(value); }
//            remove { _unpinMessageEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _unpinMessageEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> CategoryEvent
//        {
//            add { _categoryEvent.Add(value); }
//            remove { _categoryEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _categoryEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> HandleParticipantMeetStateEvent
//        {
//            add { _handleParticipantMeetStateEvent.Add(value); }
//            remove { _handleParticipantMeetStateEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _handleParticipantMeetStateEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> DeleteAccountEvent
//        {
//            add { _deleteAccountEvent.Add(value); }
//            remove { _deleteAccountEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _deleteAccountEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> EphemeralMessageSendEvent
//        {
//            add { _ephemeralMessageSendEvent.Add(value); }
//            remove { _ephemeralMessageSendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _ephemeralMessageSendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> BlockFriendEvent
//        {
//            add { _blockFriendEvent.Add(value); }
//            remove { _blockFriendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _blockFriendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> VoiceReactionSendEvent
//        {
//            add { _voiceReactionSendEvent.Add(value); }
//            remove { _voiceReactionSendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _voiceReactionSendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> MarkAsReadEvent
//        {
//            add { _markAsReadEvent.Add(value); }
//            remove { _markAsReadEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _markAsReadEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ListDataSocketEvent
//        {
//            add { _listDataSocketEvent.Add(value); }
//            remove { _listDataSocketEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _listDataSocketEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> QuickMenuEvent
//        {
//            add { _quickMenuEvent.Add(value); }
//            remove { _quickMenuEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _quickMenuEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UnBlockFriendEvent
//        {
//            add { _unBlockFriendEvent.Add(value); }
//            remove { _unBlockFriendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _unBlockFriendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> MeetParticipantEvent
//        {
//            add { _meetParticipantEvent.Add(value); }
//            remove { _meetParticipantEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _meetParticipantEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> TransferOwnershipEvent
//        {
//            add { _transferOwnershipEvent.Add(value); }
//            remove { _transferOwnershipEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _transferOwnershipEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> AddFriendEvent
//        {
//            add { _addFriendEvent.Add(value); }
//            remove { _addFriendEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _addFriendEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> BanUserEvent
//        {
//            add { _banUserEvent.Add(value); }
//            remove { _banUserEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _banUserEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ActiveArchivedThreadEvent
//        {
//            add { _activeArchivedThreadEvent.Add(value); }
//            remove { _activeArchivedThreadEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _activeArchivedThreadEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> AllowAnonymousEvent
//        {
//            add { _allowAnonymousEvent.Add(value); }
//            remove { _allowAnonymousEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _allowAnonymousEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> UpdateLocalcacheEvent
//        {
//            add { _updateLocalcacheEvent.Add(value); }
//            remove { _updateLocalcacheEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _updateLocalcacheEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> ClanCreatedEvent
//        {
//            add { _clanCreatedEvent.Add(value); }
//            remove { _clanCreatedEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _clanCreatedEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> AiagentEnabledEvent
//        {
//            add { _aiagentEnabledEvent.Add(value); }
//            remove { _aiagentEnabledEvent.Remove(value); }
//        }
//        internal readonly AsyncEvent<Func<Task>> _aiagentEnabledEvent = new AsyncEvent<Func<Task>>();
//    }
//}
