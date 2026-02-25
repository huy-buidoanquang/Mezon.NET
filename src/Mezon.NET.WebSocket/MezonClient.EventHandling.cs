using System;
using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.Protobuf.Realtime;

namespace Mezon.NET.WebSocket
{
    public partial class MezonClient
    {
        private async Task ProcessMessageAsync(SoketMessageCode code, Envelope envelope)
        {
            _lastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (code == SoketMessageCode.Ready)
            {
                await _socketLogger.DebugAsync("Received: REDY").ConfigureAwait(false);
                try
                {
                    await TimedInvokeAsync(_readyEvent, nameof(ReadyEvent)).ConfigureAwait(false);
                    _heartbeatTask = RunHeartbeatAsync(_connection.CancelToken);
                    _ = _connection.CompleteAsync();
                }
                catch (Exception ex)
                {
                    _connection.CriticalError(new Exception("Processing REDY failed", ex));
                }
                return;
            }
            else if (code == SoketMessageCode.Data)
            {
                await _socketLogger.DebugAsync("Received: DATA").ConfigureAwait(false);
                if (envelope == null)
                {
                    return;
                }

                try
                {
                    switch (envelope.MessageCase)
                    {
                        case Envelope.MessageOneofCase.None:
                            break;
                        case Envelope.MessageOneofCase.Channel:
                            await TimedInvokeAsync(_channelEvent, nameof(ChannelEvent), envelope.Channel).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanJoin:
                            await TimedInvokeAsync(_clanJoinEvent, nameof(ClanJoinEvent), envelope.ClanJoin).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelJoin:
                            await TimedInvokeAsync(_channelJoinEvent, nameof(ChannelJoinEvent), envelope.ChannelJoin).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelLeave:
                            await TimedInvokeAsync(_channelLeaveEvent, nameof(ChannelLeaveEvent), envelope.ChannelLeave).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessage:
                            await TimedInvokeAsync(_channelMessageEvent, nameof(ChannelMessageEvent), envelope.ChannelMessage).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageAck:
                            await TimedInvokeAsync(_channelMessageAckEvent, nameof(ChannelMessageAckEvent), envelope.ChannelMessageAck).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageSend:
                            await TimedInvokeAsync(_channelMessageSendEvent, nameof(ChannelMessageSendEvent), envelope.ChannelMessageSend).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageUpdate:
                            await TimedInvokeAsync(_channelMessageUpdateEvent, nameof(ChannelMessageUpdateEvent), envelope.ChannelMessageUpdate).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelMessageRemove:
                            await TimedInvokeAsync(_channelMessageRemoveEvent, nameof(ChannelMessageRemoveEvent), envelope.ChannelMessageRemove).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelPresenceEvent:
                            await TimedInvokeAsync(_channelPresenceEvent, nameof(ChannelPresenceEvent), envelope.ChannelPresenceEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Error:
                            await TimedInvokeAsync(_errorEvent, nameof(ErrorEvent), envelope.Error).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Notifications:
                            await TimedInvokeAsync(_notificationsEvent, nameof(NotificationsEvent), envelope.Notifications).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Rpc:
                            await TimedInvokeAsync(_rpcEvent, nameof(RpcEvent), envelope.Rpc).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Status:
                            await TimedInvokeAsync(_statusEvent, nameof(StatusEvent), envelope.Status).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusFollow:
                            await TimedInvokeAsync(_statusFollowEvent, nameof(StatusFollowEvent), envelope.StatusFollow).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusPresenceEvent:
                            await TimedInvokeAsync(_statusPresenceEvent, nameof(StatusPresenceEvent), envelope.StatusPresenceEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusUnfollow:
                            await TimedInvokeAsync(_statusUnfollowEvent, nameof(StatusUnfollowEvent), envelope.StatusUnfollow).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StatusUpdate:
                            await TimedInvokeAsync(_statusUpdateEvent, nameof(StatusUpdateEvent), envelope.StatusUpdate).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamData:
                            await TimedInvokeAsync(_streamDataEvent, nameof(StreamDataEvent), envelope.StreamData).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StreamPresenceEvent:
                            await TimedInvokeAsync(_streamPresenceEvent, nameof(StreamPresenceEvent), envelope.StreamPresenceEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.Ping:
                            break;
                        case Envelope.MessageOneofCase.Pong:
                            if (_heartbeatTimes.TryDequeue(out long time))
                            {
                                long latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time;
                                Latency = latency;

                                await TimedInvokeAsync(_pongEvent, nameof(PongEvent), envelope.Pong).ConfigureAwait(false);
                            }
                            break;
                        case Envelope.MessageOneofCase.MessageTypingEvent:
                            await TimedInvokeAsync(_messageTypingEvent, nameof(MessageTypingEvent), envelope.MessageTypingEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.LastSeenMessageEvent:
                            await TimedInvokeAsync(_lastSeenMessageEvent, nameof(LastSeenMessageEvent), envelope.LastSeenMessageEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MessageReactionEvent:
                            await TimedInvokeAsync(_messageReactionEvent, nameof(MessageReactionEvent), envelope.MessageReactionEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceJoinedEvent:
                            await TimedInvokeAsync(_voiceJoinedEvent, nameof(VoiceJoinedEvent), envelope.VoiceJoinedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceLeavedEvent:
                            await TimedInvokeAsync(_voiceLeavedEvent, nameof(VoiceLeavedEvent), envelope.VoiceLeavedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceStartedEvent:
                            await TimedInvokeAsync(_voiceStartedEvent, nameof(VoiceStartedEvent), envelope.VoiceStartedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceEndedEvent:
                            await TimedInvokeAsync(_voiceEndedEvent, nameof(VoiceEndedEvent), envelope.VoiceEndedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelCreatedEvent:
                            await TimedInvokeAsync(_channelCreatedEvent, nameof(ChannelCreatedEvent), envelope.ChannelCreatedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelDeletedEvent:
                            await TimedInvokeAsync(_channelDeletedEvent, nameof(ChannelDeletedEvent), envelope.ChannelDeletedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelUpdatedEvent:
                            await TimedInvokeAsync(_channelUpdatedEvent, nameof(ChannelUpdatedEvent), envelope.ChannelUpdatedEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.LastPinMessageEvent:
                            await TimedInvokeAsync(_lastPinMessageEvent, nameof(LastPinMessageEvent), envelope.LastPinMessageEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CustomStatusEvent:
                            await TimedInvokeAsync(_customStatusEvent, nameof(CustomStatusEvent), envelope.CustomStatusEvent).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserChannelAddedEvent:
                            await TimedInvokeAsync(_userChannelAddedEvent, nameof(UserChannelAddedEvent), envelope.UserChannelAddedEvent).ConfigureAwait(false);
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
                            await TimedInvokeAsync(_checkNameExistedEvent, nameof(CheckNameExistedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserProfileUpdatedEvent:
                            await TimedInvokeAsync(_userProfileUpdatedEvent, nameof(UserProfileUpdatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AddClanUserEvent:
                            await TimedInvokeAsync(_addClanUserEvent, nameof(AddClanUserEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanEventCreated:
                            await TimedInvokeAsync(_clanEventCreated, nameof(ClanEventCreated)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RoleAssignEvent:
                            await TimedInvokeAsync(_roleAssignEvent, nameof(RoleAssignEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanDeletedEvent:
                            await TimedInvokeAsync(_clanDeletedEvent, nameof(ClanDeletedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.GiveCoffeeEvent:
                            await TimedInvokeAsync(_giveCoffeeEvent, nameof(GiveCoffeeEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerCreateEvent:
                            await TimedInvokeAsync(_stickerCreateEvent, nameof(StickerCreateEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerUpdateEvent:
                            await TimedInvokeAsync(_stickerUpdateEvent, nameof(StickerUpdateEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.StickerDeleteEvent:
                            await TimedInvokeAsync(_stickerDeleteEvent, nameof(StickerDeleteEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RoleEvent:
                            await TimedInvokeAsync(_roleEvent, nameof(RoleEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.EventEmoji:
                            await TimedInvokeAsync(_eventEmoji, nameof(EventEmoji)).ConfigureAwait(false);
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
                            await TimedInvokeAsync(_permissionSetEvent, nameof(PermissionSetEvent)).ConfigureAwait(false);
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
                            await TimedInvokeAsync(_unmuteEvent, nameof(UnmuteEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.WebrtcSignalingFwd:
                            await TimedInvokeAsync(_webrtcSignalingFwdEvent, nameof(WebrtcSignalingFwdEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ListActivity:
                            await TimedInvokeAsync(_listActivityEvent, nameof(ListActivityEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.DropdownBoxSelected:
                            await TimedInvokeAsync(_dropdownBoxSelectedEvent, nameof(DropdownBoxSelectedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.IncomingCallPush:
                            await TimedInvokeAsync(_incomingCallPushEvent, nameof(IncomingCallPushEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.SdTopicEvent:
                            await TimedInvokeAsync(_sdTopicEvent, nameof(SdTopicEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.FollowEvent:
                            await TimedInvokeAsync(_followEvent, nameof(FollowEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ChannelAppEvent:
                            await TimedInvokeAsync(_channelAppEvent, nameof(ChannelAppEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UserStatusEvent:
                            await TimedInvokeAsync(_userStatusEvent, nameof(UserStatusEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.RemoveFriend:
                            await TimedInvokeAsync(_removeFriendEvent, nameof(RemoveFriendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.WebhookEvent:
                            await TimedInvokeAsync(_webhookEvent, nameof(WebhookEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.NotiUserChannel:
                            await TimedInvokeAsync(_notiUserChannelEvent, nameof(NotiUserChannelEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.JoinChannelAppData:
                            await TimedInvokeAsync(_joinChannelAppDataEvent, nameof(JoinChannelAppDataEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CanvasEvent:
                            await TimedInvokeAsync(_canvasEvent, nameof(CanvasEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UnpinMessageEvent:
                            await TimedInvokeAsync(_unpinMessageEvent, nameof(UnpinMessageEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.CategoryEvent:
                            await TimedInvokeAsync(_categoryEvent, nameof(CategoryEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.HandleParticipantMeetStateEvent:
                            await TimedInvokeAsync(_handleParticipantMeetStateEvent, nameof(HandleParticipantMeetStateEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.DeleteAccountEvent:
                            await TimedInvokeAsync(_deleteAccountEvent, nameof(DeleteAccountEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.EphemeralMessageSend:
                            await TimedInvokeAsync(_ephemeralMessageSendEvent, nameof(EphemeralMessageSendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.BlockFriend:
                            await TimedInvokeAsync(_blockFriendEvent, nameof(BlockFriendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.VoiceReactionSend:
                            await TimedInvokeAsync(_voiceReactionSendEvent, nameof(VoiceReactionSendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MarkAsRead:
                            await TimedInvokeAsync(_markAsReadEvent, nameof(MarkAsReadEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ListDataSocket:
                            await TimedInvokeAsync(_listDataSocketEvent, nameof(ListDataSocketEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.QuickMenuEvent:
                            await TimedInvokeAsync(_quickMenuEvent, nameof(QuickMenuEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UnBlockFriend:
                            await TimedInvokeAsync(_unBlockFriendEvent, nameof(UnBlockFriendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.MeetParticipantEvent:
                            await TimedInvokeAsync(_meetParticipantEvent, nameof(MeetParticipantEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.TransferOwnershipEvent:
                            await TimedInvokeAsync(_transferOwnershipEvent, nameof(TransferOwnershipEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AddFriend:
                            await TimedInvokeAsync(_addFriendEvent, nameof(AddFriendEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.BanUserEvent:
                            await TimedInvokeAsync(_banUserEvent, nameof(BanUserEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ActiveArchivedThread:
                            await TimedInvokeAsync(_activeArchivedThreadEvent, nameof(ActiveArchivedThreadEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AllowAnonymousEvent:
                            await TimedInvokeAsync(_allowAnonymousEvent, nameof(AllowAnonymousEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.UpdateLocalcacheEvent:
                            await TimedInvokeAsync(_updateLocalcacheEvent, nameof(UpdateLocalcacheEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.ClanCreatedEvent:
                            await TimedInvokeAsync(_clanCreatedEvent, nameof(ClanCreatedEvent)).ConfigureAwait(false);
                            break;
                        case Envelope.MessageOneofCase.AiagentEnabledEvent:
                            await TimedInvokeAsync(_aiagentEnabledEvent, nameof(AiagentEnabledEvent)).ConfigureAwait(false);
                            break;
                        default:
                            await _socketLogger.WarningAsync($"Unknown message type ({envelope.MessageCase})").ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await _socketLogger.ErrorAsync($"Error handling message ({envelope.MessageCase}): {ex.Message}").ConfigureAwait(false);
                }
            }
        }
    }
}
