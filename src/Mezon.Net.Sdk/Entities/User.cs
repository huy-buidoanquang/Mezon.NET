using System;
using System.Threading.Tasks;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Constants;
using Mezon.Net.Core.Entities;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class User : IUser
    {
        private readonly MezonClient _client;
        private long _dmChannelId;

        internal User(MezonClient client, long id, string? username = null, string? displayName = null, string? clanNick = null, long dmChannelId = 0)
        {
            _client = client;
            Id = id;
            Username = username;
            DisplayName = displayName;
            ClanNick = clanNick;
            _dmChannelId = dmChannelId;
        }

        public long Id { get; }
        public string? Username { get; internal set; }
        public string? DisplayName { get; internal set; }
        public string? ClanNick { get; internal set; }
        public long? DmChannelId => _dmChannelId == 0 ? (long?)null : _dmChannelId;

        internal void SetDmChannelId(long channelId) => _dmChannelId = channelId;

        public async Task<ChannelMessageAck> SendDMAsync(string content, int code = 0, RequestOptions? options = null)
        {
            if (_dmChannelId == 0)
            {
                var dm = await _client.DmChannels.CreateDmChannelAsync(_client.Engine, Id, options).ConfigureAwait(false);
                if (dm == null)
                {
                    throw new MezonEntityNotFoundException(nameof(User), Id, $"Unable to create DM channel for user {Id}.");
                }

                _dmChannelId = dm.ChannelId;
            }

            var mode = (int)ChannelStreamMode.Dm;
            var parameters = new SendChannelMessageParams(0, _dmChannelId, content, isPublic: false, mode: mode, code: code);
            return await MessageSendHelper.SendAsync(_client.ApiClient, parameters, options).ConfigureAwait(false);
        }
    }
}
