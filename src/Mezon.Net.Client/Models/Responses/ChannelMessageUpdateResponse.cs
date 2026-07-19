#nullable enable
using System;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Models
{
    /// <summary>
    ///     Allocation-free response facade for the message-update hot path.
    ///     Nested projections are materialized once when the event is decoded.
    /// </summary>
    public readonly struct ChannelMessageUpdateResponse
    {
        private static readonly ProtoListView<MessageMentionResponse> EmptyMentions =
            new ProtoListView<MessageMentionResponse>(Array.Empty<MessageMentionResponse>());
        private static readonly ProtoListView<MessageAttachmentResponse> EmptyAttachments =
            new ProtoListView<MessageAttachmentResponse>(Array.Empty<MessageAttachmentResponse>());

        private readonly ChannelMessageUpdate _proto;
        private readonly ProtoListView<MessageMentionResponse> _mentions;
        private readonly ProtoListView<MessageAttachmentResponse> _attachments;

        internal ChannelMessageUpdateResponse(ChannelMessageUpdate proto)
        {
            _proto = proto;
            _mentions = proto.Mentions.Count == 0
                ? EmptyMentions
                : ProtoListView<MessageMentionResponse>.FromRepeated(
                    proto.Mentions,
                    static mention => new MessageMentionResponse(mention));
            _attachments = proto.Attachments.Count == 0
                ? EmptyAttachments
                : ProtoListView<MessageAttachmentResponse>.FromRepeated(
                    proto.Attachments,
                    static attachment => new MessageAttachmentResponse(attachment));
        }

        internal ChannelMessageUpdate Proto => _proto;

        public long ClanId => _proto.ClanId;
        public long ChannelId => _proto.ChannelId;
        public long MessageId => _proto.MessageId;
        public string Content => _proto.Content;
        public ProtoListView<MessageMentionResponse> Mentions => _mentions;
        public ProtoListView<MessageAttachmentResponse> Attachments => _attachments;
        public int Mode => _proto.Mode;
        public bool IsPublic => _proto.IsPublic;
        public bool HideEditted => _proto.HideEditted;
        public long TopicId => _proto.TopicId;
        public bool IsUpdateMsgTopic => _proto.IsUpdateMsgTopic;
        public uint CreateTimeSeconds => _proto.CreateTimeSeconds;
    }
}
