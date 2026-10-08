using System;
using System.Threading.Tasks;
using Mezon.Net.Client.Dispatch;
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
        /// Detaches event invoke from the receive path. Ordered mode queues it on the lane of
        /// <paramref name="lane"/> (channel or clan id); concurrent mode runs it on the thread pool, where
        /// <see cref="Task.Yield"/> is required: fire-and-forget alone still runs until the first incomplete await on
        /// the caller stack.
        /// </summary>
        private void ScheduleEvent(long lane, Func<Task> invoker)
        {
            if (_dispatcher != null)
            {
                _dispatcher.Enqueue(lane, invoker);
                return;
            }

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
                var lane = _dispatcher != null ? RealtimeLaneKey.Compute(envelope) : 0;
                switch (envelope.MessageCase)
                {
                    case Envelope.MessageOneofCase.None:
                        break;
                    case Envelope.MessageOneofCase.Channel:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelReceivedEvent, nameof(ChannelReceivedEvent), new ChannelEventData(new ChannelResponse(envelope.Channel))));
                        break;
                    case Envelope.MessageOneofCase.ClanJoin:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanJoinedEvent, nameof(ClanJoinedEvent), new ClanJoinEventData(new ClanJoinResponse(envelope.ClanJoin))));
                        break;
                    case Envelope.MessageOneofCase.ChannelJoin:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelJoinedEvent, nameof(ChannelJoinedEvent), new ChannelJoinEventData(new ChannelJoinResponse(envelope.ChannelJoin))));
                        break;
                    case Envelope.MessageOneofCase.ChannelLeave:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelLeftEvent, nameof(ChannelLeftEvent), new ChannelLeaveEventData(new ChannelLeaveResponse(envelope.ChannelLeave))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessage:
                        // Decode nested mentions/attachments/references/reactions once at the engine boundary.
                        var channelMessage = ChannelMessageResponse.Decode(envelope.ChannelMessage);
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelMessageReceivedEvent, nameof(ChannelMessageReceivedEvent), new ChannelMessageEventData(channelMessage)));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageAck:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelMessageAckReceivedEvent, nameof(ChannelMessageAckReceivedEvent), new ChannelMessageAckEventData(new ChannelMessageAckResponse(envelope.ChannelMessageAck))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageSend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelMessageSentEvent, nameof(ChannelMessageSentEvent), new ChannelMessageSendEventData(new ChannelMessageSendResponse(envelope.ChannelMessageSend))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageUpdate:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelMessageUpdatedEvent, nameof(ChannelMessageUpdatedEvent), new ChannelMessageUpdateEventData(new ChannelMessageUpdateResponse(envelope.ChannelMessageUpdate))));
                        break;
                    case Envelope.MessageOneofCase.ChannelMessageRemove:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelMessageRemovedEvent, nameof(ChannelMessageRemovedEvent), new ChannelMessageRemoveEventData(new ChannelMessageRemoveResponse(envelope.ChannelMessageRemove))));
                        break;
                    case Envelope.MessageOneofCase.ChannelPresenceEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelPresenceChangedEvent, nameof(ChannelPresenceChangedEvent), new ChannelPresenceEventEventData(new ChannelPresenceEventResponse(envelope.ChannelPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.Error:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_errorReceivedEvent, nameof(ErrorReceivedEvent), new ErrorEventData(new ErrorResponse(envelope.Error))));
                        break;
                    case Envelope.MessageOneofCase.Notifications:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_notificationsReceivedEvent, nameof(NotificationsReceivedEvent), new NotificationsEventData(new Mezon.Net.Models.NotificationsResponse(envelope.Notifications))));
                        break;
                    case Envelope.MessageOneofCase.Rpc:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_rpcReceivedEvent, nameof(RpcReceivedEvent), new RpcEventData(new RpcResponse(envelope.Rpc))));
                        break;
                    case Envelope.MessageOneofCase.Status:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_statusReceivedEvent, nameof(StatusReceivedEvent), new StatusEventData(new StatusResponse(envelope.Status))));
                        break;
                    case Envelope.MessageOneofCase.StatusFollow:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_statusFollowedEvent, nameof(StatusFollowedEvent), new StatusFollowEventData(new StatusFollowResponse(envelope.StatusFollow))));
                        break;
                    case Envelope.MessageOneofCase.StatusPresenceEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_statusPresenceChangedEvent, nameof(StatusPresenceChangedEvent), new StatusPresenceEventEventData(new StatusPresenceEventResponse(envelope.StatusPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.StatusUnfollow:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_statusUnfollowedEvent, nameof(StatusUnfollowedEvent), new StatusUnfollowEventData(new StatusUnfollowResponse(envelope.StatusUnfollow))));
                        break;
                    case Envelope.MessageOneofCase.StatusUpdate:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_statusUpdatedEvent, nameof(StatusUpdatedEvent), new StatusUpdateEventData(new StatusUpdateResponse(envelope.StatusUpdate))));
                        break;
                    case Envelope.MessageOneofCase.StreamData:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamDataReceivedEvent, nameof(StreamDataReceivedEvent), new StreamDataEventData(new StreamDataResponse(envelope.StreamData))));
                        break;
                    case Envelope.MessageOneofCase.StreamPresenceEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamPresenceChangedEvent, nameof(StreamPresenceChangedEvent), new StreamPresenceEventEventData(new StreamPresenceEventResponse(envelope.StreamPresenceEvent))));
                        break;
                    case Envelope.MessageOneofCase.Ping:
                        break;
                    case Envelope.MessageOneofCase.Pong:
                        if (_heartbeatTimes.TryDequeue(out long time))
                        {
                            long latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time;
                            Latency = latency;

                            ScheduleEvent(lane, () => TimedInvokeAsync(_pongReceivedEvent, nameof(PongReceivedEvent), new PongEventData(new PongResponse(envelope.Pong))));
                        }
                        break;
                    case Envelope.MessageOneofCase.MessageTypingEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_messageTypingReceivedEvent, nameof(MessageTypingReceivedEvent), new MessageTypingEventEventData(new MessageTypingEventResponse(envelope.MessageTypingEvent))));
                        break;
                    case Envelope.MessageOneofCase.LastSeenMessageEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_lastSeenMessageUpdatedEvent, nameof(LastSeenMessageUpdatedEvent), new LastSeenMessageEventEventData(new LastSeenMessageEventResponse(envelope.LastSeenMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.MessageReactionEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_messageReactionReceivedEvent, nameof(MessageReactionReceivedEvent), new MessageReactionEventData(new MessageReactionResponse(envelope.MessageReactionEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceJoinedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceJoinedEvent, nameof(VoiceJoinedEvent), new VoiceJoinedEventEventData(new VoiceJoinedEventResponse(envelope.VoiceJoinedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceLeavedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceLeavedEvent, nameof(VoiceLeavedEvent), new VoiceLeavedEventEventData(new VoiceLeavedEventResponse(envelope.VoiceLeavedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceStartedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceStartedEvent, nameof(VoiceStartedEvent), new VoiceStartedEventEventData(new VoiceStartedEventResponse(envelope.VoiceStartedEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceEndedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceEndedEvent, nameof(VoiceEndedEvent), new VoiceEndedEventEventData(new VoiceEndedEventResponse(envelope.VoiceEndedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelCreatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelCreatedEvent, nameof(ChannelCreatedEvent), new ChannelCreatedEventEventData(new ChannelCreatedEventResponse(envelope.ChannelCreatedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelDeletedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelDeletedEvent, nameof(ChannelDeletedEvent), new ChannelDeletedEventEventData(new ChannelDeletedEventResponse(envelope.ChannelDeletedEvent))));
                        break;
                    case Envelope.MessageOneofCase.ChannelUpdatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelUpdatedEvent, nameof(ChannelUpdatedEvent), new ChannelUpdatedEventEventData(new ChannelUpdatedEventResponse(envelope.ChannelUpdatedEvent))));
                        break;
                    case Envelope.MessageOneofCase.LastPinMessageEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_lastPinMessageUpdatedEvent, nameof(LastPinMessageUpdatedEvent), new LastPinMessageEventEventData(new LastPinMessageEventResponse(envelope.LastPinMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.CustomStatusEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_customStatusChangedEvent, nameof(CustomStatusChangedEvent), new CustomStatusEventEventData(new CustomStatusEventResponse(envelope.CustomStatusEvent))));
                        break;
                    case Envelope.MessageOneofCase.UserChannelAddedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userChannelAddedEvent, nameof(UserChannelAddedEvent), new UserChannelAddedEventData(new UserChannelAddedResponse(envelope.UserChannelAddedEvent))));
                        break;
                    case Envelope.MessageOneofCase.UserChannelRemovedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userChannelRemovedEvent, nameof(UserChannelRemovedEvent), new UserChannelRemovedEventData(new UserChannelRemovedResponse(envelope.UserChannelRemovedEvent))));
                        break;
                    case Envelope.MessageOneofCase.UserClanRemovedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userClanRemovedEvent, nameof(UserClanRemovedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanUpdatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanUpdatedEvent, nameof(ClanUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ClanProfileUpdatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanProfileUpdatedEvent, nameof(ClanProfileUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CheckNameExistedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_nameExistenceCheckedEvent, nameof(NameExistenceCheckedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UserProfileUpdatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userProfileUpdatedEvent, nameof(UserProfileUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AddClanUserEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanUserAddedEvent, nameof(ClanUserAddedEvent), new AddClanUserEventEventData(new AddClanUserEventResponse(envelope.AddClanUserEvent))));
                        break;
                    case Envelope.MessageOneofCase.ClanEventCreated:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanEventCreated, nameof(ClanEventCreated)));
                        break;
                    case Envelope.MessageOneofCase.RoleAssignEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_roleAssignedEvent, nameof(RoleAssignedEvent), new RoleAssignedEventEventData(new RoleAssignedEventResponse(envelope.RoleAssignEvent))));
                        break;
                    case Envelope.MessageOneofCase.ClanDeletedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanDeletedEvent, nameof(ClanDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.GiveCoffeeEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_coffeeGivenEvent, nameof(CoffeeGivenEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerCreateEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_stickerCreatedEvent, nameof(StickerCreatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerUpdateEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_stickerUpdatedEvent, nameof(StickerUpdatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StickerDeleteEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_stickerDeletedEvent, nameof(StickerDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.RoleEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_roleChangedEvent, nameof(RoleChangedEvent), new RoleEventEventData(new RoleEventResponse(envelope.RoleEvent))));
                        break;
                    case Envelope.MessageOneofCase.EventEmoji:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_emojiReceivedEvent, nameof(EmojiReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingJoinedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamingJoinedEvent, nameof(StreamingJoinedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingLeavedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamingLeavedEvent, nameof(StreamingLeavedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingStartedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamingStartedEvent, nameof(StreamingStartedEvent)));
                        break;
                    case Envelope.MessageOneofCase.StreamingEndedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_streamingEndedEvent, nameof(StreamingEndedEvent)));
                        break;
                    case Envelope.MessageOneofCase.PermissionSetEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_permissionsSetEvent, nameof(PermissionsSetEvent)));
                        break;
                    case Envelope.MessageOneofCase.PermissionChangedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_permissionChangedEvent, nameof(PermissionChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.TokenSentEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_tokenSentEvent, nameof(TokenSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.MessageButtonClicked:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_messageButtonClickedEvent, nameof(MessageButtonClickedEvent), new MessageButtonClickedEventData(new MessageButtonClickedResponse(envelope.MessageButtonClicked))));
                        break;
                    case Envelope.MessageOneofCase.UnmuteEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userUnmutedEvent, nameof(UserUnmutedEvent)));
                        break;
                    case Envelope.MessageOneofCase.WebrtcSignalingFwd:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_webrtcSignalingForwardedEvent, nameof(WebrtcSignalingForwardedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListActivity:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_activityListedEvent, nameof(ActivityListedEvent)));
                        break;
                    case Envelope.MessageOneofCase.DropdownBoxSelected:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_dropdownBoxSelectedEvent, nameof(DropdownBoxSelectedEvent), new DropdownBoxSelectedEventData(new DropdownBoxSelectedResponse(envelope.DropdownBoxSelected))));
                        break;
                    case Envelope.MessageOneofCase.IncomingCallPush:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_incomingCallPushedEvent, nameof(IncomingCallPushedEvent)));
                        break;
                    case Envelope.MessageOneofCase.SdTopicEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_sdTopicReceivedEvent, nameof(SdTopicReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.FollowEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_followReceivedEvent, nameof(FollowReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ChannelAppEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelAppReceivedEvent, nameof(ChannelAppReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UserStatusEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userStatusChangedEvent, nameof(UserStatusChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.RemoveFriend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_friendRemovedEvent, nameof(FriendRemovedEvent)));
                        break;
                    case Envelope.MessageOneofCase.WebhookEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_webhookReceivedEvent, nameof(WebhookReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.NotiUserChannel:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_notiUserChannelReceivedEvent, nameof(NotiUserChannelReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.JoinChannelAppData:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelAppDataJoinedEvent, nameof(ChannelAppDataJoinedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CanvasEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_canvasReceivedEvent, nameof(CanvasReceivedEvent)));
                        break;
                    case Envelope.MessageOneofCase.UnpinMessageEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_messageUnpinnedEvent, nameof(MessageUnpinnedEvent)));
                        break;
                    case Envelope.MessageOneofCase.CategoryEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_categoryChangedEvent, nameof(CategoryChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.HandleParticipantMeetStateEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_participantMeetStateChangedEvent, nameof(ParticipantMeetStateChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.DeleteAccountEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_accountDeletedEvent, nameof(AccountDeletedEvent)));
                        break;
                    case Envelope.MessageOneofCase.EphemeralMessageSend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_ephemeralMessageSentEvent, nameof(EphemeralMessageSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.BlockFriend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_friendBlockedEvent, nameof(FriendBlockedEvent)));
                        break;
                    case Envelope.MessageOneofCase.VoiceReactionSend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceReactionSentEvent, nameof(VoiceReactionSentEvent)));
                        break;
                    case Envelope.MessageOneofCase.MarkAsRead:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_markedAsReadEvent, nameof(MarkedAsReadEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListDataSocket:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_dataSocketListedEvent, nameof(DataSocketListedEvent)));
                        break;
                    case Envelope.MessageOneofCase.QuickMenuEvent:
                        // The parameterless event carries no payload, so it fires for every quick menu event as before.
                        if (_quickMenuReceivedEvent.HasSubscribers)
                        {
                            ScheduleEvent(lane, () => TimedInvokeAsync(_quickMenuReceivedEvent, nameof(QuickMenuReceivedEvent)));
                        }

                        // The typed event needs a menu and a source message. ClanId is 0 in DMs, so it is not required.
                        var quickMenu = envelope.QuickMenuEvent;
                        if (_quickMenuReceivedDataEvent.HasSubscribers
                            && quickMenu is not null
                            && !string.IsNullOrWhiteSpace(quickMenu.MenuName)
                            && quickMenu.Message is not null
                            && quickMenu.Message.Id > 0
                            && quickMenu.Message.ChannelId > 0)
                        {
                            var quickMenuData = new QuickMenuReceivedEventData(quickMenu);
                            ScheduleEvent(lane, () => TimedInvokeAsync(_quickMenuReceivedDataEvent, nameof(QuickMenuReceivedDataEvent), quickMenuData));
                        }
                        break;
                    case Envelope.MessageOneofCase.UnBlockFriend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_friendUnblockedEvent, nameof(FriendUnblockedEvent)));
                        break;
                    case Envelope.MessageOneofCase.MeetParticipantEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_meetParticipantChangedEvent, nameof(MeetParticipantChangedEvent)));
                        break;
                    case Envelope.MessageOneofCase.TransferOwnershipEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_ownershipTransferredEvent, nameof(OwnershipTransferredEvent)));
                        break;
                    case Envelope.MessageOneofCase.AddFriend:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_friendAddedEvent, nameof(FriendAddedEvent)));
                        break;
                    case Envelope.MessageOneofCase.BanUserEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_userBannedEvent, nameof(UserBannedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ActiveArchivedThread:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_archivedThreadActivatedEvent, nameof(ArchivedThreadActivatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AllowAnonymousEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_anonymousAllowedEvent, nameof(AnonymousAllowedEvent)));
                        break;
                    case Envelope.MessageOneofCase.ApiRequestEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_apiRequestReceivedEvent, nameof(ApiRequestReceivedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))));
                        ScheduleEvent(lane, () => TimedInvokeAsync(_localCacheUpdatedEvent, nameof(LocalCacheUpdatedEvent), new ApiRequestEventEventData(new ApiRequestEventResponse(envelope.ApiRequestEvent))));
                        break;
                    case Envelope.MessageOneofCase.ClanCreatedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_clanCreatedEvent, nameof(ClanCreatedEvent)));
                        break;
                    case Envelope.MessageOneofCase.AiagentEnabledEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_aIAgentEnabledEvent, nameof(AIAgentEnabledEvent)));
                        break;
                    case Envelope.MessageOneofCase.ListChannelUsersBannedEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelUsersBannedListedEvent, nameof(ChannelUsersBannedListedEvent), new ListChannelUsersBannedEventEventData(new ListChannelUsersBannedEventResponse(envelope.ListChannelUsersBannedEvent))));
                        break;
                    case Envelope.MessageOneofCase.RefreshSessionEvent:
                        ScheduleEvent(lane, async () =>
                        {
                            // Apply the pushed session first so REST calls and reconnects use the new token.
                            var refreshedSession = new Session(envelope.RefreshSessionEvent);
                            await Sessions.ApplyPushedSessionAsync(refreshedSession).ConfigureAwait(false);
                            await TimedInvokeAsync(_sessionRefreshedEvent, nameof(SessionRefreshedEvent), refreshedSession).ConfigureAwait(false);
                        });
                        break;
                    case Envelope.MessageOneofCase.ChannelArchiveEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_channelArchivedEvent, nameof(ChannelArchivedEvent), new ChannelArchiveEventEventData(new ChannelArchiveEventResponse(envelope.ChannelArchiveEvent))));
                        break;
                    case Envelope.MessageOneofCase.TopicInMessageEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_topicInMessageReceivedEvent, nameof(TopicInMessageReceivedEvent), new TopicInMessageEventEventData(new TopicInMessageEventResponse(envelope.TopicInMessageEvent))));
                        break;
                    case Envelope.MessageOneofCase.ScreenShareEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_screenShareReceivedEvent, nameof(ScreenShareReceivedEvent), new ScreenShareEventEventData(new ScreenShareEventResponse(envelope.ScreenShareEvent))));
                        break;
                    case Envelope.MessageOneofCase.VoiceInteractiveEvent:
                        ScheduleEvent(lane, () => TimedInvokeAsync(_voiceInteractiveReceivedEvent, nameof(VoiceInteractiveReceivedEvent), new VoiceInteractiveEventEventData(new VoiceInteractiveEventResponse(envelope.VoiceInteractiveEvent))));
                        break;
                    default:
                        ScheduleEvent(lane, () => _logger.WarningAsync($"Unknown message type ({envelope.MessageCase})"));
                        break;
                }
            }
            catch (Exception ex)
            {
                ScheduleEvent(0, () => _logger.ErrorAsync($"Error handling message ({envelope.MessageCase}): {ex.Message}"));
            }
        }
    }
}
