using System;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        private void BindCacheListeners()
        {
            if (_cacheListenersBound)
            {
                return;
            }

            _cacheListenersBound = true;
            _engine.ChannelMessageReceivedEvent += OnChannelMessageInternalAsync;
            _engine.ChannelMessageUpdatedEvent += OnChannelMessageUpdatedInternalAsync;
            _engine.ChannelMessageRemovedEvent += OnChannelMessageRemovedInternalAsync;
            _engine.MessageReactionReceivedEvent += OnMessageReactionInternalAsync;
            _engine.ChannelCreatedEvent += OnChannelCreatedInternalAsync;
            _engine.ChannelUpdatedEvent += OnChannelUpdatedInternalAsync;
            _engine.ChannelDeletedEvent += OnChannelDeletedInternalAsync;
            _engine.UserChannelAddedEvent += OnUserChannelAddedInternalAsync;
        }

        private Task OnChannelMessageInternalAsync(ChannelMessageEventData messageEvent)
        {
            var message = (ChannelMessageResponse)messageEvent;
            if (message.ChannelId != 0 && Channels.TryGet(message.ChannelId, out var channel))
            {
                if (channel.Messages.TryGet(message.MessageId, out var existing))
                {
                    existing.UpdateFrom(message);
                }
                else
                {
                    channel.Messages.Set(message.MessageId, new Entities.Message(this, channel, message));
                }
            }

            UpdateUserFromMessage(message);
            return Task.CompletedTask;
        }

        private Task OnChannelMessageUpdatedInternalAsync(ChannelMessageUpdateEventData messageEvent)
        {
            var update = (ChannelMessageUpdateResponse)messageEvent;
            if (update.ChannelId == 0 || !Channels.TryGet(update.ChannelId, out var channel))
            {
                return Task.CompletedTask;
            }

            if (channel.Messages.TryGet(update.MessageId, out var existing))
            {
                existing.UpdateFrom(update);
            }

            return Task.CompletedTask;
        }

        private Task OnChannelMessageRemovedInternalAsync(ChannelMessageRemoveEventData messageEvent)
        {
            var remove = (ChannelMessageRemoveResponse)messageEvent;
            if (remove.ChannelId != 0 && Channels.TryGet(remove.ChannelId, out var channel))
            {
                channel.Messages.Remove(remove.MessageId);
            }

            return Task.CompletedTask;
        }

        private Task OnMessageReactionInternalAsync(MessageReactionEventData reactionEvent)
        {
            var reaction = (MessageReactionResponse)reactionEvent;
            if (reaction.ChannelId == 0 || !Channels.TryGet(reaction.ChannelId, out var channel))
            {
                return Task.CompletedTask;
            }

            if (channel.Messages.TryGet(reaction.MessageId, out var existing))
            {
                existing.ApplyReaction(reaction);
            }

            return Task.CompletedTask;
        }

        private void UpdateUserFromMessage(ChannelMessageResponse message)
        {
            if (message.SenderId == 0)
            {
                return;
            }

            if (Users.TryGet(message.SenderId, out var user))
            {
                user.Username = message.Username;
                user.DisplayName = message.DisplayName;
                user.ClanNick = message.ClanNick;
            }
            else
            {
                Users.Set(message.SenderId, new Entities.User(this, message.SenderId, message.Username, message.DisplayName, message.ClanNick));
            }
        }

        private Task OnChannelCreatedInternalAsync(ChannelCreatedEventEventData evt)
        {
            var data = (ChannelCreatedEventResponse)evt;
            UpdateChannelCache(data.ClanId, data.ChannelId, data.ChannelLabel, data.ChannelType, data.ChannelPrivate == 0);
            return Task.CompletedTask;
        }

        private Task OnChannelUpdatedInternalAsync(ChannelUpdatedEventEventData evt)
        {
            var data = (ChannelUpdatedEventResponse)evt;
            if (data.ChannelType == (int)ChannelType.Thread && data.Status == 1)
            {
                ScheduleBackground(
                    _engine.JoinChannelChatRtAsync(new ChannelJoinParams(data.ClanId, data.ChannelId, data.ChannelType, !data.ChannelPrivate)),
                    "JoinChannelChat (thread update)");
                return Task.CompletedTask;
            }

            UpdateChannelCache(data.ClanId, data.ChannelId, data.ChannelLabel, data.ChannelType, !data.ChannelPrivate);
            return Task.CompletedTask;
        }

        private Task OnChannelDeletedInternalAsync(ChannelDeletedEventEventData evt)
        {
            var data = (ChannelDeletedEventResponse)evt;
            Channels.Remove(data.ChannelId);
            return Task.CompletedTask;
        }

        private Task OnUserChannelAddedInternalAsync(UserChannelAddedEventData channelEvent)
        {
            var data = (UserChannelAddedResponse)channelEvent;
            var channelDesc = data.ChannelDesc;
            if (channelDesc.ChannelId == 0)
            {
                return Task.CompletedTask;
            }

            for (var i = 0; i < data.Users.Count; i++)
            {
                var user = data.Users[i];
                if (user.UserId == Options.BotId)
                {
                    ScheduleBackground(
                        _engine.JoinChannelChatRtAsync(new ChannelJoinParams(data.ClanId, channelDesc.ChannelId, channelDesc.Type, channelDesc.ChannelPrivate == 0)),
                        "JoinChannelChat (user channel added)");
                    break;
                }
            }

            return Task.CompletedTask;
        }

        private void ScheduleBackground(Task task, string name)
        {
            _ = ObserveFaultAsync(task, name);
        }

        private async Task ObserveFaultAsync(Task task, string name)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync($"Background {name} failed.", ex).ConfigureAwait(false);
            }
        }

        private void UpdateChannelCache(long clanId, long channelId, string label, int type, bool isPublic)
        {
            if (!Clans.TryGet(clanId, out var clan))
            {
                return;
            }

            if (Channels.TryGet(channelId, out var existing))
            {
                existing.UpdateFrom(new global::Mezon.Net.Internal.Api.ChannelDescription
                {
                    ClanId = clanId,
                    ChannelId = channelId,
                    ChannelLabel = label,
                    Type = type,
                    ChannelPrivate = isPublic ? 0 : 1,
                });
                return;
            }

            Channels.Set(channelId, new Entities.TextChannel(this, new global::Mezon.Net.Internal.Api.ChannelDescription
            {
                ClanId = clanId,
                ChannelId = channelId,
                ChannelLabel = label,
                Type = type,
                ChannelPrivate = isPublic ? 0 : 1,
            }, clan));
        }
    }
}
