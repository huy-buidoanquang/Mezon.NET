using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Entities;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Entities
{
    public sealed partial class Message : IMessage
    {
        private readonly MezonClient _client;
        private readonly TextChannel _channel;
        private ChannelMessageResponse? _source;
        private MessageContent? _content;

        internal Message(MezonClient client, TextChannel channel, ChannelMessageResponse source)
        {
            _client = client;
            _channel = channel;
            _source = source;
        }

        internal Message(MezonClient client, TextChannel channel, ChannelMessageAck ack, MessageContent content)
        {
            _client = client;
            _channel = channel;
            Ack = ack;
            _content = content;
        }

        internal ChannelMessageResponse? Source => _source;
        internal ChannelMessageAck? Ack { get; }

        public long Id => Source?.MessageId ?? Ack?.MessageId ?? 0;
        public long ClanId => _channel.ClanId;
        public long ChannelId => _channel.Id;
        public long SenderId => Source?.SenderId ?? 0;
        public MessageContent Content => _content ??= MessageContent.Parse(Source?.Content ?? string.Empty);
        public string RawContent => Content.RawJson;
        public int Code => Source?.Code ?? 0;
        public TextChannel Channel => _channel;

        string IMessage.Content => RawContent;

        public Task<ChannelMessageAckResponse> ReplyAsync(
            MessageContent content,
            long? topicId = null,
            int code = 0,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null)
        {
            var reply = new ReplyMessageParams(
                ClanId,
                ChannelId,
                content.ToJson(),
                _channel.Type,
                _channel.IsPublic,
                Id,
                SenderId,
                Source?.Username,
                Source?.Avatar,
                RawContent,
                topicId,
                code,
                mentions: mentions,
                attachments: attachments);

            return _client.SendQueue.EnqueueAsync(ChannelId, () => MessageSendHelper.SendReplyAsync(_client.Engine, reply, options));
        }

        public Task<ChannelMessageAckResponse> ReplyAsync(
            string content,
            long? topicId = null,
            int code = 0,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null)
            => ReplyAsync(MessageContent.Parse(content), topicId, code, mentions, attachments, options);

        public Task<ChannelMessageAckResponse> ReplyTextAsync(
            string text,
            long? topicId = null,
            int code = 0,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null)
            => ReplyAsync(MessageContent.CreateText(text), topicId, code, mentions, attachments, options);

        public Task UpdateAsync(
            MessageContent content,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null,
            bool hideEdited = true)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var createTime = Source?.CreateTimeSeconds is uint s and > 0
                ? s
                : Ack?.CreateTimeSeconds is uint a and > 0 ? a : (uint?)null;
            var update = new UpdateMessageParams(
                ClanId,
                ChannelId,
                Id,
                content.ToJson(),
                mode,
                _channel.IsPublic,
                hideEdited: hideEdited,
                mentions: mentions,
                attachments: attachments,
                createTimeSeconds: createTime);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.UpdateAsync(_client.Engine, update, options));
        }

        public Task UpdateAsync(
            string content,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null)
            => UpdateAsync(MessageContent.Parse(content), mentions, attachments, options);

        public Task UpdateTextAsync(
            string text,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            RequestOptions? options = null)
            => UpdateAsync(MessageContent.CreateText(text), mentions, attachments, options);

        public Task DeleteAsync(RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var delete = new DeleteMessageParams(ClanId, ChannelId, Id, mode, _channel.IsPublic);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.DeleteAsync(_client.Engine, delete, options));
        }

        public Task AddReactionAsync(long emojiId, string emoji, long senderId, RequestOptions? options = null)
            => ReactInternalAsync(emojiId, emoji, senderId, actionDelete: false, options);

        public Task RemoveReactionAsync(long emojiId, string emoji, long senderId, RequestOptions? options = null)
            => ReactInternalAsync(emojiId, emoji, senderId, actionDelete: true, options);

        /// <summary>
        ///     Prefer <see cref="AddReactionAsync"/> / <see cref="RemoveReactionAsync"/>.
        ///     <paramref name="action"/> maps to wire <c>action</c> where <see langword="true"/> means delete/remove.
        /// </summary>
        public Task ReactAsync(long emojiId, string emoji, long senderId, bool action = false, RequestOptions? options = null)
            => ReactInternalAsync(emojiId, emoji, senderId, actionDelete: action, options);

        private Task ReactInternalAsync(long emojiId, string emoji, long senderId, bool actionDelete, RequestOptions? options)
        {
            var mode = ChannelModeConverter.ToStreamMode(_channel.Type);
            var react = new ReactMessageParams(ClanId, ChannelId, Id, emojiId, emoji, mode, _channel.IsPublic, senderId, action: actionDelete);
            return _client.SendQueue.EnqueueAsync(ChannelId, () =>
                MessageSendHelper.ReactAsync(_client.Engine, react, options));
        }
    }
}
