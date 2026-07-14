using System.Threading.Tasks;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Constants;
using Mezon.Net.Core.Entities;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class Message : IMessage
    {
        private readonly MezonClient _client;
        private readonly TextChannel _channel;

        internal Message(MezonClient client, TextChannel channel, ChannelMessageResponse source)
        {
            _client = client;
            _channel = channel;
            Source = source;
        }

        internal Message(MezonClient client, TextChannel channel, ChannelMessageAck ack, string content)
        {
            _client = client;
            _channel = channel;
            Ack = ack;
            ContentOverride = content;
        }

        internal ChannelMessageResponse? Source { get; }
        internal ChannelMessageAck? Ack { get; }
        internal string? ContentOverride { get; }

        public long Id => Source?.MessageId ?? Ack?.MessageId ?? 0;
        public long ClanId => _channel.ClanId;
        public long ChannelId => _channel.Id;
        public long SenderId => Source?.SenderId ?? 0;
        public string Content => ContentOverride ?? Source?.Content ?? string.Empty;
        public int Code => Source?.Code ?? 0;
        public TextChannel Channel => _channel;

        public Task<ChannelMessageAckResponse> ReplyAsync(
            string content,
            long? topicId = null,
            int code = 0,
            RequestOptions? options = null)
        {
            var reply = new ReplyMessageParams(
                ClanId,
                ChannelId,
                content,
                _channel.Type,
                _channel.IsPublic,
                Id,
                SenderId,
                Source?.Username,
                Source?.Avatar,
                Content,
                topicId,
                code);

            return _client.SendQueue.EnqueueAsync(ChannelId, () => MessageSendHelper.SendReplyAsync(_client.Engine, reply, options));
        }

        public Task UpdateAsync(string content, RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var update = new UpdateMessageParams(ClanId, ChannelId, Id, content, mode, _channel.IsPublic);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.UpdateAsync(_client.Engine, update, options));
        }

        public Task DeleteAsync(RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var delete = new DeleteMessageParams(ClanId, ChannelId, Id, mode, _channel.IsPublic);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.DeleteAsync(_client.Engine, delete, options));
        }

        public Task ReactAsync(long emojiId, string emoji, long senderId, bool action = true, RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var react = new ReactMessageParams(ClanId, ChannelId, Id, emojiId, emoji, mode, _channel.IsPublic, senderId, action: action);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.ReactAsync(_client.Engine, react, options));
        }
    }
}
