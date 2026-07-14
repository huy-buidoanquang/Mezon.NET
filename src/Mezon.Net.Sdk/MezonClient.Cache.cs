using System.Threading.Tasks;
using Mezon.Net.Core.Constants;
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
                var entity = new Entities.Message(this, channel, message);
                channel.Messages.Set(message.MessageId, entity);
            }

            if (message.SenderId != 0)
            {
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

            return Task.CompletedTask;
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
                return _engine.JoinChannelChatRtAsync(new ChannelJoinParams(data.ClanId, data.ChannelId, data.ChannelType, !data.ChannelPrivate));
            }

            UpdateChannelCache(data.ClanId, data.ChannelId, data.ChannelLabel, data.ChannelType, !data.ChannelPrivate);
            return Task.CompletedTask;
        }

        private Task OnChannelDeletedInternalAsync(ChannelDeletedEventEventData evt)
        {
            var data = (ChannelDeletedEventResponse)evt;
            Channels.Remove(data.ChannelId);
            if (Clans.TryGet(data.ClanId, out _))
            {
                Channels.Remove(data.ChannelId);
            }

            return Task.CompletedTask;
        }

        private async Task OnUserChannelAddedInternalAsync(UserChannelAddedEventData channelEvent)
        {
            var data = (UserChannelAddedResponse)channelEvent;
            var channelDesc = data.ChannelDesc;
            if (channelDesc.ChannelId == 0)
            {
                return;
            }

            for (var i = 0; i < data.Users.Count; i++)
            {
                var user = data.Users[i];
                if (user.UserId == Options.BotId)
                {
                    await _engine.JoinChannelChatRtAsync(new ChannelJoinParams(data.ClanId, channelDesc.ChannelId, channelDesc.Type, channelDesc.ChannelPrivate == 0)).ConfigureAwait(false);
                    break;
                }
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
