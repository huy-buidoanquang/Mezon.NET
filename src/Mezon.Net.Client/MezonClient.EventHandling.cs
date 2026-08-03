using System;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client
{
    public partial class MezonClient
    {
        private Task SocketMessageHandlerAsync(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data, Envelope? envelope)
        {
            _lastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (type != MezonMessageType.Realtime || envelope == null)
            {
                return Task.CompletedTask;
            }

            // Schedule only — never await handlers on the MessageReceived / receive-loop stack.
            // Sync handler work (including empty CompletedTask subscribers) would otherwise
            // delay reading the next frame (e.g. heartbeat pong during voice join/leave bursts).
            DispatchRealtimeEnvelope(envelope);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Detaches event invoke from the receive path. <see cref="Task.Yield"/> is required:
        /// fire-and-forget alone still runs until the first incomplete await on the caller stack.
        /// </summary>
        private void ScheduleEvent(Func<Task> invoker)
        {
            _ = ObserveEventDispatchAsync(invoker);
        }

        private async Task ObserveEventDispatchAsync(Func<Task> invoke)
        {
            try
            {
                await Task.Yield();
                await invoke().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync("Realtime event dispatch failed.", ex).ConfigureAwait(false);
            }
        }

        private void DispatchRealtimeEnvelope(Envelope envelope)
        {
            try
            {
                switch (envelope.MessageCase)
                {
                    case Envelope.MessageOneofCase.None:
                        break;
                    case Envelope.MessageOneofCase.Channel:
                        ScheduleEvent(() => TimedInvokeAsync(_channelReceivedEvent, nameof(ChannelReceivedEvent), new ChannelEventData(new ChannelResponse(envelope.Channel))));
                        break;
                    case Envelope.MessageOneofCase.ClanJoin:
                        ScheduleEvent(() => TimedInvokeAsync(_clanJoinedEvent, nameof(ClanJoinedEvent), new ClanJoinEventData(new ClanJoinResponse(envelope.ClanJoin))));
                        break;
                    case Envelope.MessageOneofCase.ChannelJoin:
                        ScheduleEvent(() => TimedInvokeAsync(_channelJoinedEvent, nameof(ChannelJoinedEvent), new ChannelJoinEventData(new ChannelJoinResponse(envelope.ChannelJoin))));
                        break;
                    case Envelope.MessageOneofCase.ChannelLeave:
                        ScheduleEvent(() => TimedInvokeAsync(_channelLeftEvent, nameof(ChannelLeftEvent), new ChannelLeaveEventData(new ChannelLeaveResponse(envelope.ChannelLeave))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessage:
                        // Decode nested mentions/attachments/references/reactions once at the engine boundary.
                        var channelMessage = ChannelMessageResponse.Decode(envelope.ChannelMessage);
                        ScheduleEvent(() => TimedInvokeAsync(_channelMessageReceivedEvent, nameof(ChannelMessageReceivedEvent), new ChannelMessageEventData(channelMessage)));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageAck:
                        ScheduleEvent(() => TimedInvokeAsync(_channelMessageAckReceivedEvent, nameof(ChannelMessageAckReceivedEvent), new ChannelMessageAckEventData(new ChannelMessageAckResponse(envelope.ChannelMessageAck))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageSend:
                        ScheduleEvent(() => TimedInvokeAsync(_channelMessageSentEvent, nameof(ChannelMessageSentEvent), new ChannelMessageSendEventData(new ChannelMessageSendResponse(envelope.ChannelMessageSend))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageUpdate:
                        ScheduleEvent(() => TimedInvokeAsync(_channelMessageUpdatedEvent, nameof(ChannelMessageUpdatedEvent), new ChannelMessageUpdateEventData(new ChannelMessageUpdateResponse(envelope.ChannelMessageUpdate))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageRemove:
                        ScheduleEvent(() => TimedInvokeAsync(_channelMessageRemovedEvent, nameof(ChannelMessageRemovedEvent), new ChannelMessageRemoveEventData(new ChannelMessageRemoveResponse(envelope.ChannelMessageRemove))));
                        break;
                    case Envelope.MessageOneofCase.ChannelPresenceEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelPresenceChangedEvent, nameof(ChannelPresenceChangedEvent), new ChannelPresenceEventEventData(new ChannelPresenceEventResponse(envelope.ChannelPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.Error:
                        ScheduleEvent(() => TimedInvokeAsync(_errorReceivedEvent, nameof(ErrorReceivedEvent), new ErrorEventData(new ErrorResponse(envelope.Error))));
                        break;
                    case Envelope.MessageOneofCase.Notifications:
                        ScheduleEvent(() => TimedInvokeAsync(_notificationsReceivedEvent, nameof(NotificationsReceivedEvent), new NotificationsEventData(new Mezon.Net.Models.NotificationsResponse(envelope.Notifications))));
                        break;
                    case Envelope.MessageOneofCase.Rpc:
                        ScheduleEvent(() => TimedInvokeAsync(_rpcReceivedEvent, nameof(RpcReceivedEvent), new RpcEventData(new RpcResponse(envelope.Rpc))));
                        break;
                    case Envelope.MessageOneofCase.Status:
                        ScheduleEvent(() => TimedInvokeAsync(_statusReceivedEvent, nameof(StatusReceivedEvent), new StatusEventData(new StatusResponse(envelope.Status))));
                        break;
                    case Envelope.MessageOneofCase.StatusFollow:
                        ScheduleEvent(() => TimedInvokeAsync(_statusFollowedEvent, nameof(StatusFollowedEvent), new StatusFollowEventData(new StatusFollowResponse(envelope.StatusFollow))));
                        break;
                    case Envelope.MessageOneofCase.StatusPresenceEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_statusPresenceChangedEvent, nameof(StatusPresenceChangedEvent), new StatusPresenceEventEventData(new StatusPresenceEventResponse(envelope.StatusPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.StatusUnfollow:
                        ScheduleEvent(() => TimedInvokeAsync(_statusUnfollowedEvent, nameof(StatusUnfollowedEvent), new StatusUnfollowEventData(new StatusUnfollowResponse(envelope.StatusUnfollow))));
                        break;
                    case Envelope.MessageOneofCase.StatusUpdate:
                        ScheduleEvent(() => TimedInvokeAsync(_statusUpdatedEvent, nameof(StatusUpdatedEvent), new StatusUpdateEventData(new StatusUpdateResponse(envelope.StatusUpdate))));
                        break;
                    case Envelope.MessageOneofCase.StreamData:
                        ScheduleEvent(() => TimedInvokeAsync(_streamDataReceivedEvent, nameof(StreamDataReceivedEvent), new StreamDataEventData(new StreamDataResponse(envelope.StreamData))));
                        break;
                    case Envelope.MessageOneofCase.StreamPresenceEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_streamPresenceChangedEvent, nameof(StreamPresenceChangedEvent), new StreamPresenceEventEventData(new StreamPresenceEventResponse(envelope.StreamPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.Ping:
                        break;
                    case Envelope.MessageOneofCase.Pong:
                        if (_heartbeatTimes.TryDequeue(out long time))
                        {
                            long latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time;
                            Latency = latency;

                            ScheduleEvent(() => TimedInvokeAsync(_pongReceivedEvent, nameof(PongReceivedEvent), new PongEventData(new PongResponse(envelope.Pong))));
                        }
                        break;
                    case Envelope.MessageOneofCase.MessageTypingEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_messageTypingReceivedEvent, nameof(MessageTypingReceivedEvent), new MessageTypingEventEventData(new MessageTypingEventResponse(envelope.MessageTypingEvent))));
                        break;
                    case Envelope.MessageOneofCase.LastSeenMessageEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_lastSeenMessageUpdatedEvent, nameof(LastSeenMessageUpdatedEvent), new LastSeenMessageEventEventData(new LastSeenMessageEventResponse(envelope.LastSeenMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.MessageReactionEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_messageReactionReceivedEvent, nameof(MessageReactionReceivedEvent), new MessageReactionEventData(new MessageReactionResponse(envelope.MessageReactionEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceJoinedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_voiceJoinedEvent, nameof(VoiceJoinedEvent), new VoiceJoinedEventEventData(new VoiceJoinedEventResponse(envelope.VoiceJoinedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceLeavedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_voiceLeavedEvent, nameof(VoiceLeavedEvent), new VoiceLeavedEventEventData(new VoiceLeavedEventResponse(envelope.VoiceLeavedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceStartedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_voiceStartedEvent, nameof(VoiceStartedEvent), new VoiceStartedEventEventData(new VoiceStartedEventResponse(envelope.VoiceStartedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceEndedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_voiceEndedEvent, nameof(VoiceEndedEvent), new VoiceEndedEventEventData(new VoiceEndedEventResponse(envelope.VoiceEndedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelCreatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelCreatedEvent, nameof(ChannelCreatedEvent), new ChannelCreatedEventEventData(new ChannelCreatedEventResponse(envelope.ChannelCreatedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelDeletedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelDeletedEvent, nameof(ChannelDeletedEvent), new ChannelDeletedEventEventData(new ChannelDeletedEventResponse(envelope.ChannelDeletedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelUpdatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelUpdatedEvent, nameof(ChannelUpdatedEvent), new ChannelUpdatedEventEventData(new ChannelUpdatedEventResponse(envelope.ChannelUpdatedEvent))));
                        break;
                    case Envelope.MessageOneofCase.LastPinMessageEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_lastPinMessageUpdatedEvent, nameof(LastPinMessageUpdatedEvent), new LastPinMessageEventEventData(new LastPinMessageEventResponse(envelope.LastPinMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.CustomStatusEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_customStatusChangedEvent, nameof(CustomStatusChangedEvent), new CustomStatusEventEventData(new CustomStatusEventResponse(envelope.CustomStatusEvent))));
                        break;
                    case Envelope.MessageOneofCase.UserChannelAddedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userChannelAddedEvent, nameof(UserChannelAddedEvent), new UserChannelAddedEventData(new UserChannelAddedResponse(envelope.UserChannelAddedEvent))));
                        break;
                    case Envelope.MessageOneofCase.UserChannelRemovedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userChannelRemovedEvent, nameof(UserChannelRemovedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UserClanRemovedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userClanRemovedEvent, nameof(UserClanRemovedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanUpdatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_clanUpdatedEvent, nameof(ClanUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanProfileUpdatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_clanProfileUpdatedEvent, nameof(ClanProfileUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CheckNameExistedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_nameExistenceCheckedEvent, nameof(NameExistenceCheckedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UserProfileUpdatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userProfileUpdatedEvent, nameof(UserProfileUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AddClanUserEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_clanUserAddedEvent, nameof(ClanUserAddedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanEventCreated:
                        ScheduleEvent(() => TimedInvokeAsync(_clanEventCreated, nameof(ClanEventCreated)));
                        break;
                    case Envelope.MessageOneofCase.RoleAssignEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_roleAssignedEvent, nameof(RoleAssignedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanDeletedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_clanDeletedEvent, nameof(ClanDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.GiveCoffeeEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_coffeeGivenEvent, nameof(CoffeeGivenEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerCreateEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_stickerCreatedEvent, nameof(StickerCreatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerUpdateEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_stickerUpdatedEvent, nameof(StickerUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerDeleteEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_stickerDeletedEvent, nameof(StickerDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.RoleEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_roleChangedEvent, nameof(RoleChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.EventEmoji:
                        ScheduleEvent(() => TimedInvokeAsync(_emojiReceivedEvent, nameof(EmojiReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingJoinedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_streamingJoinedEvent, nameof(StreamingJoinedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingLeavedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_streamingLeavedEvent, nameof(StreamingLeavedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingStartedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_streamingStartedEvent, nameof(StreamingStartedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingEndedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_streamingEndedEvent, nameof(StreamingEndedEvent)));
                        break;
                    case Envelope.MessageOneofCase.PermissionSetEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_permissionsSetEvent, nameof(PermissionsSetEvent)));
                        break;
                    case Envelope.MessageOneofCase.PermissionChangedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_permissionChangedEvent, nameof(PermissionChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.TokenSentEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_tokenSentEvent, nameof(TokenSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.MessageButtonClicked:
                        ScheduleEvent(() => TimedInvokeAsync(_messageButtonClickedEvent, nameof(MessageButtonClickedEvent), new MessageButtonClickedEventData(new MessageButtonClickedResponse(envelope.MessageButtonClicked))));
                        break;
                    case Envelope.MessageOneofCase.UnmuteEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userUnmutedEvent, nameof(UserUnmutedEvent)));
                        break;
                    case Envelope.MessageOneofCase.WebrtcSignalingFwd:
                        ScheduleEvent(() => TimedInvokeAsync(_webrtcSignalingForwardedEvent, nameof(WebrtcSignalingForwardedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListActivity:
                        ScheduleEvent(() => TimedInvokeAsync(_activityListedEvent, nameof(ActivityListedEvent)));
                        break;
                    case Envelope.MessageOneofCase.DropdownBoxSelected:
                        ScheduleEvent(() => TimedInvokeAsync(_dropdownBoxSelectedEvent, nameof(DropdownBoxSelectedEvent), new DropdownBoxSelectedEventData(new DropdownBoxSelectedResponse(envelope.DropdownBoxSelected))));
                        break;
                    case Envelope.MessageOneofCase.IncomingCallPush:
                        ScheduleEvent(() => TimedInvokeAsync(_incomingCallPushedEvent, nameof(IncomingCallPushedEvent)));
                        break;
                    case Envelope.MessageOneofCase.SdTopicEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_sdTopicReceivedEvent, nameof(SdTopicReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.FollowEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_followReceivedEvent, nameof(FollowReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ChannelAppEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelAppReceivedEvent, nameof(ChannelAppReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UserStatusEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userStatusChangedEvent, nameof(UserStatusChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.RemoveFriend:
                        ScheduleEvent(() => TimedInvokeAsync(_friendRemovedEvent, nameof(FriendRemovedEvent)));
                        break;
                    case Envelope.MessageOneofCase.WebhookEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_webhookReceivedEvent, nameof(WebhookReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.NotiUserChannel:
                        ScheduleEvent(() => TimedInvokeAsync(_notiUserChannelReceivedEvent, nameof(NotiUserChannelReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.JoinChannelAppData:
                        ScheduleEvent(() => TimedInvokeAsync(_channelAppDataJoinedEvent, nameof(ChannelAppDataJoinedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CanvasEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_canvasReceivedEvent, nameof(CanvasReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UnpinMessageEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_messageUnpinnedEvent, nameof(MessageUnpinnedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CategoryEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_categoryChangedEvent, nameof(CategoryChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.HandleParticipantMeetStateEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_participantMeetStateChangedEvent, nameof(ParticipantMeetStateChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.DeleteAccountEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_accountDeletedEvent, nameof(AccountDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.EphemeralMessageSend:
                        ScheduleEvent(() => TimedInvokeAsync(_ephemeralMessageSentEvent, nameof(EphemeralMessageSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.BlockFriend:
                        ScheduleEvent(() => TimedInvokeAsync(_friendBlockedEvent, nameof(FriendBlockedEvent)));
                        break;
                    case Envelope.MessageOneofCase.VoiceReactionSend:
                        ScheduleEvent(() => TimedInvokeAsync(_voiceReactionSentEvent, nameof(VoiceReactionSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.MarkAsRead:
                        ScheduleEvent(() => TimedInvokeAsync(_markedAsReadEvent, nameof(MarkedAsReadEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListDataSocket:
                        ScheduleEvent(() => TimedInvokeAsync(_dataSocketListedEvent, nameof(DataSocketListedEvent)));
                        break;
                    case Envelope.MessageOneofCase.QuickMenuEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_quickMenuReceivedEvent, nameof(QuickMenuReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UnBlockFriend:
                        ScheduleEvent(() => TimedInvokeAsync(_friendUnblockedEvent, nameof(FriendUnblockedEvent)));
                        break;
                    case Envelope.MessageOneofCase.MeetParticipantEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_meetParticipantChangedEvent, nameof(MeetParticipantChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.TransferOwnershipEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_ownershipTransferredEvent, nameof(OwnershipTransferredEvent)));
                        break;
                    case Envelope.MessageOneofCase.AddFriend:
                        ScheduleEvent(() => TimedInvokeAsync(_friendAddedEvent, nameof(FriendAddedEvent)));
                        break;
                    case Envelope.MessageOneofCase.BanUserEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_userBannedEvent, nameof(UserBannedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ActiveArchivedThread:
                        ScheduleEvent(() => TimedInvokeAsync(_archivedThreadActivatedEvent, nameof(ArchivedThreadActivatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AllowAnonymousEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_anonymousAllowedEvent, nameof(AnonymousAllowedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ApiRequestEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_apiRequestReceivedEvent, nameof(ApiRequestReceivedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))));
                        ScheduleEvent(() => TimedInvokeAsync(_localCacheUpdatedEvent, nameof(LocalCacheUpdatedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))));
                        break;
                    case Envelope.MessageOneofCase.ClanCreatedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_clanCreatedEvent, nameof(ClanCreatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AiagentEnabledEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_aIAgentEnabledEvent, nameof(AIAgentEnabledEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListChannelUsersBannedEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelUsersBannedListedEvent, nameof(ChannelUsersBannedListedEvent), new ListChannelUsersBannedEventEventData(new ListChannelUsersBannedEventResponse(envelope.ListChannelUsersBannedEvent))));
                        break;
                    case Envelope.MessageOneofCase.RefreshSessionEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_sessionRefreshedEvent, nameof(SessionRefreshedEvent), new Session(envelope.RefreshSessionEvent)));
                        break;
                    case Envelope.MessageOneofCase.ChannelArchiveEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_channelArchivedEvent, nameof(ChannelArchivedEvent), new ChannelArchiveEventEventData(new ChannelArchiveEventResponse(envelope.ChannelArchiveEvent))));
                        break;
                    case Envelope.MessageOneofCase.TopicInMessageEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_topicInMessageReceivedEvent, nameof(TopicInMessageReceivedEvent), new TopicInMessageEventEventData(new TopicInMessageEventResponse(envelope.TopicInMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.ScreenShareEvent:
                        ScheduleEvent(() => TimedInvokeAsync(_screenShareReceivedEvent, nameof(ScreenShareReceivedEvent), new ScreenShareEventEventData(new ScreenShareEventResponse(envelope.ScreenShareEvent))));
                        break;
                    default:
                        ScheduleEvent(() => _logger.WarningAsync($"Unknown message type ({envelope.MessageCase})"));
                        break;
                }
            }
            catch (Exception ex)
            {
                ScheduleEvent(() => _logger.ErrorAsync($"Error handling message ({envelope.MessageCase}): {ex.Message}"));
            }
        }
    }
}
