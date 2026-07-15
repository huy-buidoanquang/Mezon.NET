using System.Threading.Tasks;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Entities;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Caching;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class TextChannel : IChannel
    {
        private global::Mezon.Net.Internal.Api.ChannelDescription _desc;
        private readonly MezonClient _client;

        internal TextChannel(MezonClient client, global::Mezon.Net.Internal.Api.ChannelDescription desc, Clan clan)
        {
            _client = client;
            _desc = desc;
            Clan = clan;
            Messages = new EntityCache<Message>(_client.Options.CacheCapacity);
        }

        public long Id => _desc.ChannelId;
        public long ClanId => _desc.ClanId;
        public int Type => _desc.Type;
        public bool IsPrivate => _desc.ChannelPrivate != 0;
        public string? Name => _desc.ChannelLabel;
        public Clan Clan { get; }
        public EntityCache<Message> Messages { get; }

        internal void UpdateFrom(global::Mezon.Net.Internal.Api.ChannelDescription desc) => _desc = desc;

        public bool IsPublic => !IsPrivate;

        public Task JoinAsync() => _client.Engine.JoinChannelChatRtAsync(new ChannelJoinParams(ClanId, Id, Type, IsPublic));

        public Task<ChannelMessageAckResponse> SendAsync(
            string content,
            long? topicId = null,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false,
            RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(Type);
            var parameters = new SendChannelMessageParams(ClanId, Id, content, topicId, IsPublic, mode, code, mentionEveryone, anonymousMessage);
            return _client.SendQueue.EnqueueAsync(Id, () =>
                MessageSendHelper.SendAsync(_client.Engine, parameters, options));
        }

        public Task<ChannelMessageAckResponse> SendEphemeralAsync(
            string content,
            long receiverId,
            RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(Type);
            var parameters = new SendChannelMessageParams(ClanId, Id, content, isPublic: IsPublic, mode: mode);
            var body = new SendEphemeralMessageParams(new[] { receiverId }, parameters);
            return _client.SendQueue.EnqueueAsync(Id, async () =>
            {
                return await _client.Engine.SendEphemeralMessageRtAsync(body, options).ConfigureAwait(false);
            });
        }
    }
}
