using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Constants;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Managers
{
    public sealed class DmChannelManager
    {
        private readonly ConcurrentDictionary<long, long> _userToChannelId = new();
        private readonly List<ChannelDescription> _dmChannelDescs = new();

        public IReadOnlyList<ChannelDescription> DmChannelDescriptions => _dmChannelDescs;

        public async Task InitializeAsync(MezonClient client, RequestOptions? options = null)
        {
            _userToChannelId.Clear();
            _dmChannelDescs.Clear();

            var channels = await client.ListChannelDescsAsync(
                new ListChannelDescsParams(clanId: 0, channelType: (int)ChannelType.Dm),
                options).ConfigureAwait(false);

            if (channels.Channeldesc.Count == 0)
            {
                return;
            }

            for (var i = 0; i < channels.Channeldesc.Count; i++)
            {
                var channel = channels.Channeldesc[i].Proto;
                if (channel.Type != (int)ChannelType.Dm || channel.ChannelId == 0 || channel.UserIds.Count == 0)
                {
                    continue;
                }

                _dmChannelDescs.Add(channel);
                _userToChannelId[channel.UserIds[0]] = channel.ChannelId;
            }
        }

        public bool TryGetDmChannelId(long userId, out long channelId)
            => _userToChannelId.TryGetValue(userId, out channelId);

        public async Task<ChannelDescription?> CreateDmChannelAsync(
            MezonClient client,
            long userId,
            RequestOptions? options = null)
        {
            if (userId <= 0)
            {
                return null;
            }

            if (TryGetDmChannelId(userId, out var existingChannelId))
            {
                return _dmChannelDescs.Find(c => c.ChannelId == existingChannelId);
            }

            var channelData = await client.CreateChannelDescAsync(
                new CreateChannelDescParams(
                    clanId: 0,
                    channelId: 0,
                    categoryId: 0,
                    type: (int)ChannelType.Dm,
                    channelPrivate: 1,
                    userIds: new long?[] { userId }),
                options).ConfigureAwait(false);
            var channel = channelData.Proto;
            if (channel.ChannelId == 0)
            {
                return null;
            }

            await client.JoinChannelChatRtAsync(new ChannelJoinParams(channel.ClanId, channel.ChannelId, channel.Type, isPublic: false)).ConfigureAwait(false);

            _dmChannelDescs.Add(channel);
            _userToChannelId[userId] = channel.ChannelId;
            return channel;
        }
    }
}
