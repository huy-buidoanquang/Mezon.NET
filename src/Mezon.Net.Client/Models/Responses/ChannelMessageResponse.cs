#nullable enable
using System;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Mezon.Net.Internal.Api;

namespace Mezon.Net.Models
{
    /// <summary>
    /// Public view over <see cref="ChannelMessage"/>. Nested <c>bytes</c> payloads
    /// (<c>mentions</c>/<c>attachments</c>/<c>references</c>/<c>reactions</c>) are decoded
    /// once with protobuf-or-JSON fallback (TS <c>decodeMentions</c> parity). Malformed
    /// metadata does not drop the message.
    /// </summary>
    public readonly struct ChannelMessageResponse
    {
        private static readonly ProtoListView<MessageMentionResponse> EmptyMentions =
            new ProtoListView<MessageMentionResponse>(Array.Empty<MessageMentionResponse>());
        private static readonly ProtoListView<MessageAttachmentResponse> EmptyAttachments =
            new ProtoListView<MessageAttachmentResponse>(Array.Empty<MessageAttachmentResponse>());
        private static readonly ProtoListView<MessageRefResponse> EmptyReferences =
            new ProtoListView<MessageRefResponse>(Array.Empty<MessageRefResponse>());
        private static readonly ProtoListView<MessageReactionResponse> EmptyReactions =
            new ProtoListView<MessageReactionResponse>(Array.Empty<MessageReactionResponse>());

        private static readonly JsonParser JsonParser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        private readonly ChannelMessage _proto;
        private readonly ProtoListView<MessageMentionResponse> _mentions;
        private readonly ProtoListView<MessageAttachmentResponse> _attachments;
        private readonly ProtoListView<MessageRefResponse> _references;
        private readonly ProtoListView<MessageReactionResponse> _reactions;

        /// <summary>
        /// Engine entry-point: decode nested list payloads once before raising events / returning API results.
        /// </summary>
        internal static ChannelMessageResponse Decode(ChannelMessage proto)
            => new ChannelMessageResponse(proto);

        internal ChannelMessageResponse(ChannelMessage proto)
        {
            _proto = proto;
            _mentions = DecodeMentions(proto.Mentions);
            _attachments = DecodeAttachments(proto.Attachments);
            _references = DecodeReferences(proto.References);
            _reactions = DecodeReactions(proto.Reactions);
        }

        internal ChannelMessage Proto => _proto;

        public long ClanId => _proto.ClanId;
        public long ChannelId => _proto.ChannelId;
        public long MessageId => _proto.MessageId;
        public int Code => _proto.Code;
        public long SenderId => _proto.SenderId;
        public string Username => _proto.Username;
        public string Avatar => _proto.Avatar;
        public string Content => _proto.Content;
        public string ChannelLabel => _proto.ChannelLabel;
        public string ClanLogo => _proto.ClanLogo;
        public string CategoryName => _proto.CategoryName;
        public string DisplayName => _proto.DisplayName;
        public string ClanNick => _proto.ClanNick;
        public string ClanAvatar => _proto.ClanAvatar;
        public ProtoListView<MessageReactionResponse> Reactions => _reactions;
        public ProtoListView<MessageMentionResponse> Mentions => _mentions;
        public ProtoListView<MessageAttachmentResponse> Attachments => _attachments;
        public ProtoListView<MessageRefResponse> References => _references;
        /// <summary>Raw nested payload; no stable wrapper type in api.proto yet.</summary>
        public ReadOnlyMemory<byte> ReferencedMessage => _proto.ReferencedMessage.Memory;
        public uint CreateTimeSeconds => _proto.CreateTimeSeconds;
        public uint UpdateTimeSeconds => _proto.UpdateTimeSeconds;
        public int Mode => _proto.Mode;
        public bool HideEditted => _proto.HideEditted;
        public bool IsPublic => _proto.IsPublic;
        public long TopicId => _proto.TopicId;

        private static ProtoListView<MessageMentionResponse> DecodeMentions(ByteString bytes)
            => DecodeList(
                bytes,
                EmptyMentions,
                static bytes => MessageMentionList.Parser.ParseFrom(bytes),
                static json => JsonParser.Parse<MessageMentionList>(NormalizeListJson(json, "mentions")),
                static list => MapRepeated(list.Mentions, static x => new MessageMentionResponse(x), EmptyMentions));

        private static ProtoListView<MessageAttachmentResponse> DecodeAttachments(ByteString bytes)
            => DecodeList(
                bytes,
                EmptyAttachments,
                static bytes => MessageAttachmentList.Parser.ParseFrom(bytes),
                static json => JsonParser.Parse<MessageAttachmentList>(NormalizeListJson(json, "attachments")),
                static list => MapRepeated(list.Attachments, static x => new MessageAttachmentResponse(x), EmptyAttachments));

        private static ProtoListView<MessageRefResponse> DecodeReferences(ByteString bytes)
            => DecodeList(
                bytes,
                EmptyReferences,
                static bytes => MessageRefList.Parser.ParseFrom(bytes),
                static json => JsonParser.Parse<MessageRefList>(NormalizeListJson(json, "refs")),
                static list => MapRepeated(list.Refs, static x => new MessageRefResponse(x), EmptyReferences));

        private static ProtoListView<MessageReactionResponse> DecodeReactions(ByteString bytes)
            => DecodeList(
                bytes,
                EmptyReactions,
                static bytes => MessageReactionList.Parser.ParseFrom(bytes),
                static json => JsonParser.Parse<MessageReactionList>(NormalizeListJson(json, "reactions")),
                static list => MapRepeated(list.Reactions, static x => new MessageReactionResponse(x), EmptyReactions));

        private static ProtoListView<TData> DecodeList<TList, TData>(
            ByteString bytes,
            ProtoListView<TData> empty,
            Func<ByteString, TList> parseProto,
            Func<string, TList> parseJson,
            Func<TList, ProtoListView<TData>> map)
        {
            if (bytes.IsEmpty)
            {
                return empty;
            }

            var span = bytes.Span;
            try
            {
                if (LooksLikeJson(span))
                {
                    return map(parseJson(Encoding.UTF8.GetString(span)));
                }

                return map(parseProto(bytes));
            }
            catch
            {
                try
                {
                    return map(parseJson(Encoding.UTF8.GetString(span)));
                }
                catch
                {
                    // Malformed metadata must not drop the outer message.
                    return empty;
                }
            }
        }

        private static bool LooksLikeJson(ReadOnlySpan<byte> span)
        {
            for (var i = 0; i < span.Length; i++)
            {
                var b = span[i];
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
                {
                    continue;
                }

                // '[' or '{'
                return b == 91 || b == 123;
            }

            return false;
        }

        /// <summary>
        /// Accepts either a bare JSON array or an object wrapping the repeated field.
        /// </summary>
        private static string NormalizeListJson(string json, string fieldName)
        {
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return "{\"" + fieldName + "\":" + json + "}";
            }

            return json;
        }

        private static ProtoListView<TData> MapRepeated<TProto, TData>(
            RepeatedField<TProto> field,
            Func<TProto, TData> factory,
            ProtoListView<TData> empty)
        {
            if (field is null || field.Count == 0)
            {
                return empty;
            }

            return ProtoListView<TData>.FromRepeated(field, factory);
        }
    }
}
