using System.Threading.Tasks;
using Mezon.Net.Core.Constants;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;

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

        private Task OnChannelMessageInternalAsync(ChannelMessage message)
        {
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

        private Task OnChannelCreatedInternalAsync(ChannelCreatedEvent evt)
        {
            UpdateChannelCache(evt.ClanId, evt.ChannelId, evt.ChannelLabel, evt.ChannelType, evt.ChannelPrivate == 0);
            return Task.CompletedTask;
        }

        private Task OnChannelUpdatedInternalAsync(ChannelUpdatedEvent evt)
        {
            if (evt.ChannelType == (int)ChannelType.Thread && evt.Status == 1)
            {
                return _engine.JoinChannelChat(evt.ClanId, evt.ChannelId, evt.ChannelType, !evt.ChannelPrivate);
            }

            UpdateChannelCache(evt.ClanId, evt.ChannelId, evt.ChannelLabel, evt.ChannelType, !evt.ChannelPrivate);
            return Task.CompletedTask;
        }

        private Task OnChannelDeletedInternalAsync(ChannelDeletedEvent evt)
        {
            Channels.Remove(evt.ChannelId);
            if (Clans.TryGet(evt.ClanId, out _))
            {
                Channels.Remove(evt.ChannelId);
            }

            return Task.CompletedTask;
        }

        private async Task OnUserChannelAddedInternalAsync(UserChannelAdded channelEvent)
        {
            if (channelEvent.ChannelDesc == null)
            {
                return;
            }

            foreach (var user in channelEvent.Users)
            {
                if (user.UserId == Options.BotId)
                {
                    var desc = channelEvent.ChannelDesc;
                    await _engine.JoinChannelChat(channelEvent.ClanId, desc.ChannelId, desc.Type, desc.ChannelPrivate == 0).ConfigureAwait(false);
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
