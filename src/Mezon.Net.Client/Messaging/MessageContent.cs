using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Opt-in typed view of Mezon channel-message <c>content</c> JSON
    /// (<c>IMessageSendPayload</c> / <c>ChannelMessageContent</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wire remains a string on receive/send models. Call <see cref="Parse"/> or
    /// <see cref="CreateText"/> when you need typed access; the socket path does not parse eagerly.
    /// </para>
    /// <para>
    /// Typed roots: <c>t</c>, <c>hg</c>, <c>ej</c>, <c>lk</c>, <c>mk</c>, <c>vk</c>, <c>embed</c>,
    /// <c>components</c>. Poll, call-log, canvas metadata and message flags are also typed;
    /// Bold / code / pre / YouTube markers may be represented under
    /// <c>mk</c>; the legacy root arrays <c>pre</c>, <c>bm</c> and <c>lky</c> are also exposed
    /// as typed properties for wire compatibility. Unknown future roots still round-trip via
    /// <see cref="RawJson"/> and appear in <see cref="UnknownExtensions"/>.
    /// </para>
    /// <para>
    /// <see cref="Parse"/> only normalizes JSON. Typed arrays materialize lazily on first access
    /// (except <see cref="Text"/>, which uses a Utf8JsonReader fast-path).
    /// <see cref="ToJson"/> returns the original normalized raw string unless the instance was built
    /// from a snapshot (<see cref="CreateText"/>).
    /// </para>
    /// </remarks>
    public sealed class MessageContent
    {
        private readonly string _rawJson;
        private MessageContentSnapshot? _snapshot;
        private readonly bool _serializeFromSnapshot;
        private string? _fastText;
        private bool _fastTextResolved;

        private MessageContent(string rawJson, MessageContentSnapshot? snapshot = null, bool serializeFromSnapshot = false)
        {
            _rawJson = rawJson;
            _snapshot = snapshot;
            _serializeFromSnapshot = serializeFromSnapshot;
        }

        internal static MessageContent FromSnapshot(in MessageContentSnapshot snapshot)
        {
            var rawJson = MessageContentCodec.Serialize(snapshot);
            return new MessageContent(rawJson, snapshot, serializeFromSnapshot: true);
        }

        /// <summary>Normalized content JSON kept for send / round-trip.</summary>
        public string RawJson => _rawJson;

        /// <summary>
        /// Plain text (<c>t</c>). Prefer this over other typed properties when only the body is needed —
        /// it avoids materializing embeds, components, and token arrays.
        /// </summary>
        public string? Text
        {
            get
            {
                var snapshot = Volatile.Read(ref _snapshot);
                if (snapshot is not null)
                {
                    return snapshot.Text;
                }

                if (!Volatile.Read(ref _fastTextResolved))
                {
                    _fastText = MessageContentCodec.TryReadTextProperty(_rawJson);
                    Volatile.Write(ref _fastTextResolved, true);
                }

                return _fastText;
            }
        }

        /// <summary>Hashtag tokens (<c>hg</c>).</summary>
        public IReadOnlyList<HashtagOnMessage>? Hashtags => Snapshot.Hashtags;

        /// <summary>Custom emoji tokens (<c>ej</c>).</summary>
        public IReadOnlyList<EmojiOnMessage>? Emojis => Snapshot.Emojis;

        /// <summary>Plain link span tokens (<c>lk</c>).</summary>
        public IReadOnlyList<LinkOnMessage>? Links => Snapshot.Links;

        /// <summary>
        /// Markdown / backtick tokens (<c>mk</c>). Discriminate with <see cref="MarkdownOnMessage.Type"/>
        /// (<see cref="MarkdownMarkerType"/>).
        /// </summary>
        public IReadOnlyList<MarkdownOnMessage>? Markdown => Snapshot.Markdown;

        /// <summary>Legacy preformatted spans (<c>pre</c>).</summary>
        public IReadOnlyList<PreOnMessage>? Pre => Snapshot.Pre;

        /// <summary>Legacy bold spans (<c>bm</c>).</summary>
        public IReadOnlyList<BoldOnMessage>? Bold => Snapshot.Bold;

        /// <summary>Voice-room link span tokens (<c>vk</c>).</summary>
        public IReadOnlyList<LinkVoiceRoomOnMessage>? VoiceLinks => Snapshot.VoiceLinks;

        /// <summary>YouTube link spans (<c>lky</c>).</summary>
        public IReadOnlyList<LinkYoutubeOnMessage>? YoutubeLinks => Snapshot.YoutubeLinks;

        /// <summary>End-to-end encryption flag (<c>e2ee</c>).</summary>
        public int? E2ee => Snapshot.E2ee;

        /// <summary>Canvas payload (<c>canvas</c>) preserved as JSON.</summary>
        public JsonElement? Canvas => Snapshot.Canvas;

        /// <summary>Canvas title map (<c>cvtt</c>).</summary>
        public IReadOnlyDictionary<string, string>? CanvasTitles => Snapshot.CanvasTitles;

        /// <summary>Voice or direct-message call log metadata (<c>callLog</c>).</summary>
        public MessageCallLog? CallLog => Snapshot.CallLog;

        /// <summary>Message type metadata (<c>tp</c>).</summary>
        public string? Type => Snapshot.Type;

        /// <summary>Channel metadata (<c>cid</c>).</summary>
        public string? ChannelId => Snapshot.ChannelId;

        /// <summary>Forwarded-message flag (<c>fwd</c>).</summary>
        public bool? Forwarded => Snapshot.Forwarded;

        /// <summary>Card rendering flag (<c>isCard</c>).</summary>
        public bool? IsCard => Snapshot.IsCard;

        /// <summary>Reply count or reference metadata (<c>rpl</c>).</summary>
        public long? ReplyToMessageId => Snapshot.ReplyToMessageId;

        /// <summary>Last-seen topic metadata (<c>lsnt</c>).</summary>
        public long? LastSeenSeconds => Snapshot.LastSeenSeconds;

        /// <summary>Native poll payload.</summary>
        public MessagePoll? Poll => Snapshot.Poll;

        /// <summary>Upload completion keys (<c>presign_finish</c>).</summary>
        public IReadOnlyList<string>? PresignFinish => Snapshot.PresignFinish;

        /// <summary>Message creation timestamp metadata (<c>create_time_seconds</c>).</summary>
        public long? CreateTimeSeconds => Snapshot.CreateTimeSeconds;

        /// <summary>Embed payloads (<c>embed</c>).</summary>
        public IReadOnlyList<MessageEmbed>? Embeds => Snapshot.Embeds;

        /// <summary>Action-row components (<c>components</c>).</summary>
        public IReadOnlyList<MessageActionRow>? Components => Snapshot.Components;

        /// <summary>
        /// Root properties that are not typed above (poll, canvas, legacy markers, …).
        /// Populated only after the typed snapshot is built.
        /// </summary>
        public IReadOnlyDictionary<string, JsonElement>? UnknownExtensions => Snapshot.Unknown;

        /// <summary>Parsed once; concurrent first readers may both parse, but all observe the same published instance.</summary>
        private MessageContentSnapshot Snapshot
        {
            get
            {
                var snapshot = Volatile.Read(ref _snapshot);
                if (snapshot is not null)
                {
                    return snapshot;
                }

                snapshot = MessageContentCodec.ParseSnapshot(_rawJson);
                return Interlocked.CompareExchange(ref _snapshot, snapshot, null) ?? snapshot;
            }
        }

        /// <summary>Creates content whose JSON is exactly <c>{"t":text}</c>.</summary>
        public static MessageContent CreateText(string text)
        {
            var rawJson = MessageContentCodec.WriteText(text);
            var snapshot = new MessageContentSnapshot(
                text,
                hashtags: null,
                emojis: null,
                links: null,
                markdown: null,
                voiceLinks: null,
                embeds: null,
                components: null,
                unknown: null);
            return new MessageContent(rawJson, snapshot, serializeFromSnapshot: true);
        }

        /// <summary>
        /// Normalizes <paramref name="rawJson"/> into an object payload without building typed arrays.
        /// Malformed input becomes <c>{"t":…}</c>.
        /// </summary>
        public static MessageContent Parse(string rawJson)
        {
            if (rawJson is null)
            {
                throw new ArgumentNullException(nameof(rawJson));
            }

            var normalized = MessageContentCodec.NormalizeRawJson(rawJson);
            return new MessageContent(normalized);
        }

        /// <summary>Like <see cref="Parse"/>; returns <see langword="false"/> when <paramref name="rawJson"/> is null.</summary>
        public static bool TryParse(string? rawJson, out MessageContent content)
        {
            content = null!;
            if (rawJson is null)
            {
                return false;
            }

            try
            {
                content = Parse(rawJson);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Wire JSON. Parsed instances return <see cref="RawJson"/> unchanged; created instances
        /// serialize from the in-memory snapshot.
        /// </summary>
        public string ToJson()
        {
            if (_serializeFromSnapshot && _snapshot is not null)
            {
                return MessageContentCodec.Serialize(_snapshot);
            }

            return _rawJson;
        }

        /// <inheritdoc />
        public override string ToString() => ToJson();
    }
}
