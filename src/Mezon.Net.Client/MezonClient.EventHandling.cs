using System;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client
{
    public partial class MezonClient
    {
        private async Task ProcessMessageAsync(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data, Envelope? envelope)
        {
            _lastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (type == MezonMessageType.Realtime)
            {
                //await _logger.DebugAsync("Received: ABRIDGED").ConfigureAwait(false);
                if (envelope == null)
                {
                    return;
                }
                //await _logger.DebugAsync("Envelop:" + envelope?.ToString()).ConfigureAwait(false);

                try
                {
                    switch (envelope?.MessageCase)
                    {
                        case Envelope.MessageOneofCase.None:
                            break;
                        case Envelope.MessageOneofCase.Channel:
                            await TimedInvokeAsync(_channelReceivedEvent, nameof(ChannelReceivedEvent), new ChannelEventData(new ChannelResponse(envelope.Channel))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanJoin:
                            await TimedInvokeAsync(_clanJoinedEvent, nameof(ClanJoinedEvent), new ClanJoinEventData(new ClanJoinResponse(envelope.ClanJoin))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelJoin:
                            await TimedInvokeAsync(_channelJoinedEvent, nameof(ChannelJoinedEvent), new ChannelJoinEventData(new ChannelJoinResponse(envelope.ChannelJoin))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelLeave:
                            await TimedInvokeAsync(_channelLeftEvent, nameof(ChannelLeftEvent), new ChannelLeaveEventData(new ChannelLeaveResponse(envelope.ChannelLeave))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessage:
                            // Decode nested mentions/attachments/references/reactions once at the engine boundary.
                            var channelMessage = ChannelMessageResponse.Decode(envelope.ChannelMessage);
                            await TimedInvokeAsync(_channelMessageReceivedEvent, nameof(ChannelMessageReceivedEvent), new ChannelMessageEventData(channelMessage)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageAck:
                            await TimedInvokeAsync(_channelMessageAckReceivedEvent, nameof(ChannelMessageAckReceivedEvent), new ChannelMessageAckEventData(new ChannelMessageAckResponse(envelope.ChannelMessageAck))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageSend:
                            await TimedInvokeAsync(_channelMessageSentEvent, nameof(ChannelMessageSentEvent), new ChannelMessageSendEventData(new ChannelMessageSendResponse(envelope.ChannelMessageSend))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageUpdate:
                            await TimedInvokeAsync(_channelMessageUpdatedEvent, nameof(ChannelMessageUpdatedEvent), new ChannelMessageUpdateEventData(new ChannelMessageUpdateResponse(envelope.ChannelMessageUpdate))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageRemove:
                            await TimedInvokeAsync(_channelMessageRemovedEvent, nameof(ChannelMessageRemovedEvent), new ChannelMessageRemoveEventData(new ChannelMessageRemoveResponse(envelope.ChannelMessageRemove))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelPresenceEvent:
                            await TimedInvokeAsync(_channelPresenceChangedEvent, nameof(ChannelPresenceChangedEvent), new ChannelPresenceEventEventData(new ChannelPresenceEventResponse(envelope.ChannelPresenceEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Error:
                            await TimedInvokeAsync(_errorReceivedEvent, nameof(ErrorReceivedEvent), new ErrorEventData(new ErrorResponse(envelope.Error))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Notifications:
                            await TimedInvokeAsync(_notificationsReceivedEvent, nameof(NotificationsReceivedEvent), new NotificationsEventData(new Mezon.Net.Models.NotificationsResponse(envelope.Notifications))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Rpc:
                            await TimedInvokeAsync(_rpcReceivedEvent, nameof(RpcReceivedEvent), new RpcEventData(new RpcResponse(envelope.Rpc))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Status:
                            await TimedInvokeAsync(_statusReceivedEvent, nameof(StatusReceivedEvent), new StatusEventData(new StatusResponse(envelope.Status))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusFollow:
                            await TimedInvokeAsync(_statusFollowedEvent, nameof(StatusFollowedEvent), new StatusFollowEventData(new StatusFollowResponse(envelope.StatusFollow))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusPresenceEvent:
                            await TimedInvokeAsync(_statusPresenceChangedEvent, nameof(StatusPresenceChangedEvent), new StatusPresenceEventEventData(new StatusPresenceEventResponse(envelope.StatusPresenceEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusUnfollow:
                            await TimedInvokeAsync(_statusUnfollowedEvent, nameof(StatusUnfollowedEvent), new StatusUnfollowEventData(new StatusUnfollowResponse(envelope.StatusUnfollow))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusUpdate:
                            await TimedInvokeAsync(_statusUpdatedEvent, nameof(StatusUpdatedEvent), new StatusUpdateEventData(new StatusUpdateResponse(envelope.StatusUpdate))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamData:
                            await TimedInvokeAsync(_streamDataReceivedEvent, nameof(StreamDataReceivedEvent), new StreamDataEventData(new StreamDataResponse(envelope.StreamData))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamPresenceEvent:
                            await TimedInvokeAsync(_streamPresenceChangedEvent, nameof(StreamPresenceChangedEvent), new StreamPresenceEventEventData(new StreamPresenceEventResponse(envelope.StreamPresenceEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Ping:
                            break;
                        case Envelope.MessageOneofCase.Pong:
                            if (_heartbeatTimes.TryDequeue(out long time))
                            {
                                long latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time;
                                Latency = latency;

                                await TimedInvokeAsync(_pongReceivedEvent, nameof(PongReceivedEvent), new PongEventData(new PongResponse(envelope.Pong))).ConfigureAwait(false);
                            }
                            break;
                        case Envelope.MessageOneofCase.MessageTypingEvent:
                            await TimedInvokeAsync(_messageTypingReceivedEvent, nameof(MessageTypingReceivedEvent), new MessageTypingEventEventData(new MessageTypingEventResponse(envelope.MessageTypingEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.LastSeenMessageEvent:
                            await TimedInvokeAsync(_lastSeenMessageUpdatedEvent, nameof(LastSeenMessageUpdatedEvent), new LastSeenMessageEventEventData(new LastSeenMessageEventResponse(envelope.LastSeenMessageEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MessageReactionEvent:
                            await TimedInvokeAsync(_messageReactionReceivedEvent, nameof(MessageReactionReceivedEvent), new MessageReactionEventData(new MessageReactionResponse(envelope.MessageReactionEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceJoinedEvent:
                            await TimedInvokeAsync(_voiceJoinedEvent, nameof(VoiceJoinedEvent), new VoiceJoinedEventEventData(new VoiceJoinedEventResponse(envelope.VoiceJoinedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceLeavedEvent:
                            await TimedInvokeAsync(_voiceLeavedEvent, nameof(VoiceLeavedEvent), new VoiceLeavedEventEventData(new VoiceLeavedEventResponse(envelope.VoiceLeavedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceStartedEvent:
                            await TimedInvokeAsync(_voiceStartedEvent, nameof(VoiceStartedEvent), new VoiceStartedEventEventData(new VoiceStartedEventResponse(envelope.VoiceStartedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceEndedEvent:
                            await TimedInvokeAsync(_voiceEndedEvent, nameof(VoiceEndedEvent), new VoiceEndedEventEventData(new VoiceEndedEventResponse(envelope.VoiceEndedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelCreatedEvent:
                            await TimedInvokeAsync(_channelCreatedEvent, nameof(ChannelCreatedEvent), new ChannelCreatedEventEventData(new ChannelCreatedEventResponse(envelope.ChannelCreatedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelDeletedEvent:
                            await TimedInvokeAsync(_channelDeletedEvent, nameof(ChannelDeletedEvent), new ChannelDeletedEventEventData(new ChannelDeletedEventResponse(envelope.ChannelDeletedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelUpdatedEvent:
                            await TimedInvokeAsync(_channelUpdatedEvent, nameof(ChannelUpdatedEvent), new ChannelUpdatedEventEventData(new ChannelUpdatedEventResponse(envelope.ChannelUpdatedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.LastPinMessageEvent:
                            await TimedInvokeAsync(_lastPinMessageUpdatedEvent, nameof(LastPinMessageUpdatedEvent), new LastPinMessageEventEventData(new LastPinMessageEventResponse(envelope.LastPinMessageEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CustomStatusEvent:
                            await TimedInvokeAsync(_customStatusChangedEvent, nameof(CustomStatusChangedEvent), new CustomStatusEventEventData(new CustomStatusEventResponse(envelope.CustomStatusEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserChannelAddedEvent:
                            await TimedInvokeAsync(_userChannelAddedEvent, nameof(UserChannelAddedEvent), new UserChannelAddedEventData(new UserChannelAddedResponse(envelope.UserChannelAddedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserChannelRemovedEvent:
                            await TimedInvokeAsync(_userChannelRemovedEvent, nameof(UserChannelRemovedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserClanRemovedEvent:
                            await TimedInvokeAsync(_userClanRemovedEvent, nameof(UserClanRemovedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanUpdatedEvent:
                            await TimedInvokeAsync(_clanUpdatedEvent, nameof(ClanUpdatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanProfileUpdatedEvent:
                            await TimedInvokeAsync(_clanProfileUpdatedEvent, nameof(ClanProfileUpdatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CheckNameExistedEvent:
                            await TimedInvokeAsync(_nameExistenceCheckedEvent, nameof(NameExistenceCheckedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserProfileUpdatedEvent:
                            await TimedInvokeAsync(_userProfileUpdatedEvent, nameof(UserProfileUpdatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AddClanUserEvent:
                            await TimedInvokeAsync(_clanUserAddedEvent, nameof(ClanUserAddedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanEventCreated:
                            await TimedInvokeAsync(_clanEventCreated, nameof(ClanEventCreated)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RoleAssignEvent:
                            await TimedInvokeAsync(_roleAssignedEvent, nameof(RoleAssignedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanDeletedEvent:
                            await TimedInvokeAsync(_clanDeletedEvent, nameof(ClanDeletedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.GiveCoffeeEvent:
                            await TimedInvokeAsync(_coffeeGivenEvent, nameof(CoffeeGivenEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerCreateEvent:
                            await TimedInvokeAsync(_stickerCreatedEvent, nameof(StickerCreatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerUpdateEvent:
                            await TimedInvokeAsync(_stickerUpdatedEvent, nameof(StickerUpdatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerDeleteEvent:
                            await TimedInvokeAsync(_stickerDeletedEvent, nameof(StickerDeletedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RoleEvent:
                            await TimedInvokeAsync(_roleChangedEvent, nameof(RoleChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.EventEmoji:
                            await TimedInvokeAsync(_emojiReceivedEvent, nameof(EmojiReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamingJoinedEvent:
                            await TimedInvokeAsync(_streamingJoinedEvent, nameof(StreamingJoinedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamingLeavedEvent:
                            await TimedInvokeAsync(_streamingLeavedEvent, nameof(StreamingLeavedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamingStartedEvent:
                            await TimedInvokeAsync(_streamingStartedEvent, nameof(StreamingStartedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamingEndedEvent:
                            await TimedInvokeAsync(_streamingEndedEvent, nameof(StreamingEndedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.PermissionSetEvent:
                            await TimedInvokeAsync(_permissionsSetEvent, nameof(PermissionsSetEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.PermissionChangedEvent:
                            await TimedInvokeAsync(_permissionChangedEvent, nameof(PermissionChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.TokenSentEvent:
                            await TimedInvokeAsync(_tokenSentEvent, nameof(TokenSentEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MessageButtonClicked:
                            await TimedInvokeAsync(_messageButtonClickedEvent, nameof(MessageButtonClickedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UnmuteEvent:
                            await TimedInvokeAsync(_userUnmutedEvent, nameof(UserUnmutedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.WebrtcSignalingFwd:
                            await TimedInvokeAsync(_webrtcSignalingForwardedEvent, nameof(WebrtcSignalingForwardedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ListActivity:
                            await TimedInvokeAsync(_activityListedEvent, nameof(ActivityListedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.DropdownBoxSelected:
                            await TimedInvokeAsync(_dropdownBoxSelectedEvent, nameof(DropdownBoxSelectedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.IncomingCallPush:
                            await TimedInvokeAsync(_incomingCallPushedEvent, nameof(IncomingCallPushedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.SdTopicEvent:
                            await TimedInvokeAsync(_sdTopicReceivedEvent, nameof(SdTopicReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.FollowEvent:
                            await TimedInvokeAsync(_followReceivedEvent, nameof(FollowReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelAppEvent:
                            await TimedInvokeAsync(_channelAppReceivedEvent, nameof(ChannelAppReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserStatusEvent:
                            await TimedInvokeAsync(_userStatusChangedEvent, nameof(UserStatusChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RemoveFriend:
                            await TimedInvokeAsync(_friendRemovedEvent, nameof(FriendRemovedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.WebhookEvent:
                            await TimedInvokeAsync(_webhookReceivedEvent, nameof(WebhookReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.NotiUserChannel:
                            await TimedInvokeAsync(_notiUserChannelReceivedEvent, nameof(NotiUserChannelReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.JoinChannelAppData:
                            await TimedInvokeAsync(_channelAppDataJoinedEvent, nameof(ChannelAppDataJoinedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CanvasEvent:
                            await TimedInvokeAsync(_canvasReceivedEvent, nameof(CanvasReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UnpinMessageEvent:
                            await TimedInvokeAsync(_messageUnpinnedEvent, nameof(MessageUnpinnedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CategoryEvent:
                            await TimedInvokeAsync(_categoryChangedEvent, nameof(CategoryChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.HandleParticipantMeetStateEvent:
                            await TimedInvokeAsync(_participantMeetStateChangedEvent, nameof(ParticipantMeetStateChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.DeleteAccountEvent:
                            await TimedInvokeAsync(_accountDeletedEvent, nameof(AccountDeletedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.EphemeralMessageSend:
                            await TimedInvokeAsync(_ephemeralMessageSentEvent, nameof(EphemeralMessageSentEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.BlockFriend:
                            await TimedInvokeAsync(_friendBlockedEvent, nameof(FriendBlockedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceReactionSend:
                            await TimedInvokeAsync(_voiceReactionSentEvent, nameof(VoiceReactionSentEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MarkAsRead:
                            await TimedInvokeAsync(_markedAsReadEvent, nameof(MarkedAsReadEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ListDataSocket:
                            await TimedInvokeAsync(_dataSocketListedEvent, nameof(DataSocketListedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.QuickMenuEvent:
                            await TimedInvokeAsync(_quickMenuReceivedEvent, nameof(QuickMenuReceivedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UnBlockFriend:
                            await TimedInvokeAsync(_friendUnblockedEvent, nameof(FriendUnblockedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MeetParticipantEvent:
                            await TimedInvokeAsync(_meetParticipantChangedEvent, nameof(MeetParticipantChangedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.TransferOwnershipEvent:
                            await TimedInvokeAsync(_ownershipTransferredEvent, nameof(OwnershipTransferredEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AddFriend:
                            await TimedInvokeAsync(_friendAddedEvent, nameof(FriendAddedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.BanUserEvent:
                            await TimedInvokeAsync(_userBannedEvent, nameof(UserBannedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ActiveArchivedThread:
                            await TimedInvokeAsync(_archivedThreadActivatedEvent, nameof(ArchivedThreadActivatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AllowAnonymousEvent:
                            await TimedInvokeAsync(_anonymousAllowedEvent, nameof(AnonymousAllowedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ApiRequestEvent:
                            await TimedInvokeAsync(_apiRequestReceivedEvent, nameof(ApiRequestReceivedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))).ConfigureAwait(false);
                            await TimedInvokeAsync(_localCacheUpdatedEvent, nameof(LocalCacheUpdatedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanCreatedEvent:
                            await TimedInvokeAsync(_clanCreatedEvent, nameof(ClanCreatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AiagentEnabledEvent:
                            await TimedInvokeAsync(_aIAgentEnabledEvent, nameof(AIAgentEnabledEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ListChannelUsersBannedEvent:
                            await TimedInvokeAsync(_channelUsersBannedListedEvent, nameof(ChannelUsersBannedListedEvent), new ListChannelUsersBannedEventEventData(new ListChannelUsersBannedEventResponse(envelope.ListChannelUsersBannedEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RefreshSessionEvent:
                            await TimedInvokeAsync(_sessionRefreshedEvent, nameof(SessionRefreshedEvent), new Session(envelope.RefreshSessionEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelArchiveEvent:
                            await TimedInvokeAsync(_channelArchivedEvent, nameof(ChannelArchivedEvent), new ChannelArchiveEventEventData(new ChannelArchiveEventResponse(envelope.ChannelArchiveEvent))).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.TopicInMessageEvent:
                            await TimedInvokeAsync(_topicInMessageReceivedEvent, nameof(TopicInMessageReceivedEvent), new TopicInMessageEventEventData(new TopicInMessageEventResponse(envelope.TopicInMessageEvent))).ConfigureAwait(false);
                            break;
                        default:
                            await _logger.WarningAsync($"Unknown message type ({envelope?.MessageCase})").ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await _logger.ErrorAsync($"Error handling message ({envelope?.MessageCase}): {ex.Message}").ConfigureAwait(false);
                }
            }
        }
    }
}
