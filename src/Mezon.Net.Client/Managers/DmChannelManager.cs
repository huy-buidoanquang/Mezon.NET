using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Core.Constants;
using Mezon.Net.Internal.Api;

namespace Mezon.Net.Client.Managers
{
    public sealed class DmChannelManager
    {
        private readonly ConcurrentDictionary<long, long> _userToChannelId = new();
        private readonly List<ChannelDescription> _dmChannelDescs = new();

        public IReadOnlyList<ChannelDescription> DmChannelDescriptions => _dmChannelDescs;

        public async Task InitializeAsync(IMezonApiClient api, MezonClient client, RequestOptions? options = null)
        {
            _userToChannelId.Clear();
            _dmChannelDescs.Clear();

            var channels = await client.ListChannelDescsAsync(
                clanId: 0,
                channelType: (int)ChannelType.Dm,
                options: options).ConfigureAwait(false);

            if (channels?.Channeldesc == null || channels.Channeldesc.Count == 0)
            {
                return;
            }

            foreach (var channel in channels.Channeldesc)
            {
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
            IMezonApiClient api,
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

            var request = new CreateChannelDescRequest
            {
                ClanId = 0,
                ChannelId = 0,
                CategoryId = 0,
                Type = (int)ChannelType.Dm,
                ChannelPrivate = 1,
            };
            request.UserIds.Add(userId);

            var channel = await api.CreateChannelDescAsync(request, options).ConfigureAwait(false);
            if (channel == null || channel.ChannelId == 0)
            {
                return null;
            }

            await client.JoinChannelChat(channel.ClanId, channel.ChannelId, channel.Type, isPublic: false).ConfigureAwait(false);

            _dmChannelDescs.Add(channel);
            _userToChannelId[userId] = channel.ChannelId;
            return channel;
        }
    }
}
