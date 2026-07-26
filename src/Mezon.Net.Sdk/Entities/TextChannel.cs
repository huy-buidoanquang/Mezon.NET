using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Entities;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Caching;
using Mezon.Net.Client;

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
        /// <summary>LiveKit / meet room name for voice channels (used by STN playMedia).</summary>
        public string MeetingCode => _desc.MeetingCode;
        public Clan Clan { get; }
        public EntityCache<Message> Messages { get; }

        internal void UpdateFrom(global::Mezon.Net.Internal.Api.ChannelDescription desc) => _desc = desc;

        public bool IsPublic => !IsPrivate;

        /// <summary>
        /// Explicit channel presence join. Not required for public clan channels after <c>JoinClanChat</c>
        /// (server maps public sends onto the clan stream). Useful for DM/group/private/thread edge cases.
        /// </summary>
        public Task JoinAsync() => _client.Engine.JoinChannelChatRtAsync(new ChannelJoinParams(ClanId, Id, Type, IsPublic));

        public Task<ChannelMessageAckResponse> SendAsync(
            MessageContent content,
            long? topicId = null,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(Type);
            var parameters = new SendChannelMessageParams(
                ClanId,
                Id,
                content.ToJson(),
                topicId,
                IsPublic,
                mode,
                code,
                mentionEveryone,
                anonymousMessage,
                mentions,
                attachments,
                references);
            return _client.SendQueue.EnqueueAsync(Id, () =>
                MessageSendHelper.SendAsync(_client.Engine, parameters, options));
        }

        public Task<ChannelMessageAckResponse> SendAsync(
            string content,
            long? topicId = null,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
            => SendAsync(MessageContent.Parse(content), topicId, code, mentionEveryone, anonymousMessage, mentions, attachments, references, options);

        public Task<ChannelMessageAckResponse> SendTextAsync(
            string text,
            long? topicId = null,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
            => SendAsync(MessageContent.CreateText(text), topicId, code, mentionEveryone, anonymousMessage, mentions, attachments, references, options);

        public Task<ChannelMessageAckResponse> SendEphemeralAsync(
            MessageContent content,
            long receiverId,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
        {
            var mode = ChannelModeConverter.ToStreamMode(Type);
            var parameters = new SendChannelMessageParams(
                ClanId,
                Id,
                content.ToJson(),
                isPublic: IsPublic,
                mode: mode,
                mentions: mentions,
                attachments: attachments,
                references: references);
            var body = new SendEphemeralMessageParams(new[] { receiverId }, parameters);
            return _client.SendQueue.EnqueueAsync(Id, async () =>
            {
                return await _client.Engine.SendEphemeralMessageRtAsync(body, options).ConfigureAwait(false);
            });
        }

        public Task<ChannelMessageAckResponse> SendEphemeralAsync(
            string content,
            long receiverId,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
            => SendEphemeralAsync(MessageContent.Parse(content), receiverId, mentions, attachments, references, options);

        public Task<ChannelMessageAckResponse> SendEphemeralTextAsync(
            string text,
            long receiverId,
            IEnumerable<MessageMentionParams>? mentions = null,
            IEnumerable<MessageAttachmentParams>? attachments = null,
            IEnumerable<MessageRefParams>? references = null,
            RequestOptions? options = null)
            => SendEphemeralAsync(MessageContent.CreateText(text), receiverId, mentions, attachments, references, options);
    }
}
