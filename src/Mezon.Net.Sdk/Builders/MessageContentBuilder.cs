using System;
using System.Collections.Generic;
using System.Text.Json;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Builders;

/// <summary>
/// Builds the complete typed message-content codec supported by Mezon.Net.
/// </summary>
/// <remarks>
/// The builder covers text (<c>t</c>), channel/emoji/link/markdown/voice markers
/// (<c>hg</c>, <c>ej</c>, <c>lk</c>, <c>mk</c>, legacy <c>pre</c>/<c>bm</c>, <c>vk</c>, <c>lky</c>), embeds, action rows and
/// forward-compatible unknown root properties. It delegates final serialization to
/// the same codec used by <see cref="MessageContent.Parse(string)"/>.
/// </remarks>
public sealed class MessageContentBuilder
{
    private readonly List<HashtagOnMessage> _hashtags = new();
    private readonly List<EmojiOnMessage> _emojis = new();
    private readonly List<LinkOnMessage> _links = new();
    private readonly List<MarkdownOnMessage> _markdown = new();
    private readonly List<PreOnMessage> _pre = new();
    private readonly List<BoldOnMessage> _bold = new();
    private readonly List<LinkVoiceRoomOnMessage> _voiceLinks = new();
    private readonly List<LinkYoutubeOnMessage> _youtubeLinks = new();
    private readonly List<MessageEmbed> _embeds = new();
    private readonly List<MessageActionRow> _components = new();
    private readonly Dictionary<string, JsonElement> _unknown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _canvasTitles = new(StringComparer.Ordinal);
    private bool _built;
    private string? _text;
    private int? _e2ee;
    private JsonElement? _canvas;
    private MessageCallLog? _callLog;
    private string? _type;
    private string? _channelId;
    private bool? _forwarded;
    private bool? _isCard;
    private long? _replyToMessageId;
    private long? _lastSeenSeconds;
    private MessagePoll? _poll;
    private IReadOnlyList<string>? _presignFinish;
    private long? _createTimeSeconds;

    public MessageContentBuilder SetText(string? text)
    {
        EnsureMutable();
        _text = text;
        return this;
    }

    public MessageContentBuilder SetE2ee(int? value)
    {
        EnsureMutable();
        _e2ee = value;
        return this;
    }

    public MessageContentBuilder SetCanvas(JsonElement? canvas)
    {
        EnsureMutable();
        _canvas = canvas?.Clone();
        return this;
    }

    public MessageContentBuilder SetCanvasTitles(IReadOnlyDictionary<string, string>? titles)
    {
        EnsureMutable();
        _canvasTitles.Clear();
        if (titles is not null)
        {
            foreach (var pair in titles)
            {
                _canvasTitles[pair.Key] = pair.Value;
            }
        }

        return this;
    }

    public MessageContentBuilder SetCallLog(MessageCallLog? callLog)
    {
        EnsureMutable();
        _callLog = callLog;
        return this;
    }

    public MessageContentBuilder SetCallLog(MessageCallLogBuilder callLog)
    {
        if (callLog is null)
        {
            throw new ArgumentNullException(nameof(callLog));
        }

        return SetCallLog(callLog.Build());
    }

    public MessageContentBuilder SetType(string? type)
    {
        EnsureMutable();
        _type = type;
        return this;
    }

    public MessageContentBuilder SetChannelId(string? channelId)
    {
        EnsureMutable();
        _channelId = channelId;
        return this;
    }

    public MessageContentBuilder SetForwarded(bool? forwarded)
    {
        EnsureMutable();
        _forwarded = forwarded;
        return this;
    }

    public MessageContentBuilder SetIsCard(bool? isCard)
    {
        EnsureMutable();
        _isCard = isCard;
        return this;
    }

    public MessageContentBuilder SetReplyToMessageId(long? replyToMessageId)
    {
        EnsureMutable();
        _replyToMessageId = replyToMessageId;
        return this;
    }

    public MessageContentBuilder SetLastSeenSeconds(long? lastSeenSeconds)
    {
        EnsureMutable();
        _lastSeenSeconds = lastSeenSeconds;
        return this;
    }

    public MessageContentBuilder SetPoll(MessagePoll? poll)
    {
        EnsureMutable();
        _poll = poll;
        return this;
    }

    public MessageContentBuilder SetPoll(MessagePollBuilder poll)
    {
        if (poll is null)
        {
            throw new ArgumentNullException(nameof(poll));
        }

        return SetPoll(poll.Build());
    }

    public MessageContentBuilder SetPresignFinish(IReadOnlyList<string>? keys)
    {
        EnsureMutable();
        _presignFinish = keys is null || keys.Count == 0 ? null : new List<string>(keys).ToArray();
        return this;
    }

    public MessageContentBuilder SetCreateTimeSeconds(long? createTimeSeconds)
    {
        EnsureMutable();
        _createTimeSeconds = createTimeSeconds;
        return this;
    }

    public MessageContentBuilder AddHashtag(string? channelId, int? start = null, int? end = null)
    {
        EnsureMutable();
        _hashtags.Add(new HashtagOnMessage(channelId, start, end));
        return this;
    }

    public MessageContentBuilder AddHashtag(HashtagOnMessage marker)
    {
        EnsureMutable();
        _hashtags.Add(marker);
        return this;
    }

    public MessageContentBuilder AddEmoji(string? emojiId, int? start = null, int? end = null)
    {
        EnsureMutable();
        _emojis.Add(new EmojiOnMessage(emojiId, start, end));
        return this;
    }

    public MessageContentBuilder AddEmoji(EmojiOnMessage marker)
    {
        EnsureMutable();
        _emojis.Add(marker);
        return this;
    }

    public MessageContentBuilder AddLink(int? start = null, int? end = null)
    {
        EnsureMutable();
        _links.Add(new LinkOnMessage(start, end));
        return this;
    }

    public MessageContentBuilder AddLink(LinkOnMessage marker)
    {
        EnsureMutable();
        _links.Add(marker);
        return this;
    }

    public MessageContentBuilder AddMarkdown(
        string? type,
        int? start = null,
        int? end = null,
        string? url = null,
        string? language = null,
        IReadOnlyDictionary<string, JsonElement>? extensions = null)
    {
        EnsureMutable();
        ValidateExtensionNames(extensions, static name => name is "type" or "s" or "e" or "url" or "language");
        _markdown.Add(new MarkdownOnMessage(type, start, end, url, language, CloneExtensions(extensions)));
        return this;
    }

    public MessageContentBuilder AddMarkdown(MarkdownOnMessage marker)
    {
        EnsureMutable();
        ValidateExtensionNames(marker.Extensions, static name => name is "type" or "s" or "e" or "url" or "language");
        _markdown.Add(new MarkdownOnMessage(
            marker.Type,
            marker.Start,
            marker.End,
            marker.Url,
            marker.Language,
            CloneExtensions(marker.Extensions)));
        return this;
    }

    public MessageContentBuilder AddVoiceLink(int? start = null, int? end = null)
    {
        EnsureMutable();
        _voiceLinks.Add(new LinkVoiceRoomOnMessage(start, end));
        return this;
    }

    public MessageContentBuilder AddVoiceLink(LinkVoiceRoomOnMessage marker)
    {
        EnsureMutable();
        _voiceLinks.Add(marker);
        return this;
    }

    public MessageContentBuilder AddPre(string? language = null, int? start = null, int? end = null)
    {
        EnsureMutable();
        _pre.Add(new PreOnMessage(language, start, end));
        return this;
    }

    public MessageContentBuilder AddPre(PreOnMessage marker)
    {
        EnsureMutable();
        _pre.Add(marker);
        return this;
    }

    public MessageContentBuilder AddBold(string? language = null, int? start = null, int? end = null)
    {
        EnsureMutable();
        _bold.Add(new BoldOnMessage(language, start, end));
        return this;
    }

    public MessageContentBuilder AddBold(BoldOnMessage marker)
    {
        EnsureMutable();
        _bold.Add(marker);
        return this;
    }

    public MessageContentBuilder AddYoutubeLink(int? start = null, int? end = null)
    {
        EnsureMutable();
        _youtubeLinks.Add(new LinkYoutubeOnMessage(start, end));
        return this;
    }

    public MessageContentBuilder AddYoutubeLink(LinkYoutubeOnMessage marker)
    {
        EnsureMutable();
        _youtubeLinks.Add(marker);
        return this;
    }

    public MessageContentBuilder AddEmbed(MessageEmbed embed)
    {
        EnsureMutable();
        if (embed is null)
        {
            throw new ArgumentNullException(nameof(embed));
        }
        _embeds.Add(embed);
        return this;
    }

    public MessageContentBuilder AddActionRow(MessageActionRow row)
    {
        EnsureMutable();
        if (row is null)
        {
            throw new ArgumentNullException(nameof(row));
        }
        _components.Add(row);
        return this;
    }

    public MessageContentBuilder AddActionRow(IReadOnlyList<MessageComponent> components)
        => AddActionRow(new MessageActionRow(components));

    public MessageContentBuilder SetExtension(string name, JsonElement value)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name is required.", nameof(name));
        }

        if (name is "t" or "e2ee" or "hg" or "ej" or "lk" or "mk" or "pre" or "bm" or "vk" or "lky"
            or "embed" or "components" or "canvas" or "cvtt" or "callLog" or "tp" or "cid" or "fwd"
            or "isCard" or "rpl" or "lsnt" or "question" or "question_emoji_id" or "answers"
            or "answer_emoji_ids" or "answer_counts" or "expire_time" or "is_closed" or "total_votes"
            or "allow_multiple_answers" or "user_votes" or "id" or "expire_at" or "type"
            or "presign_finish" or "create_time_seconds")
        {
            throw new ArgumentException($"'{name}' is a known message property; use its typed builder method.", nameof(name));
        }

        _unknown[name] = value.Clone();
        return this;
    }

    public MessageContent Build()
    {
        EnsureMutable();
        _built = true;
        var snapshot = new MessageContentSnapshot(
            _text,
            ToNullable(_hashtags),
            ToNullable(_emojis),
            ToNullable(_links),
            ToNullable(_markdown),
            ToNullable(_voiceLinks),
            ToNullable(_embeds),
            ToNullable(_components),
            _unknown.Count == 0 ? null : new Dictionary<string, JsonElement>(_unknown, StringComparer.Ordinal),
            ToNullable(_pre),
            ToNullable(_bold),
            ToNullable(_youtubeLinks),
            _e2ee,
            _canvas,
            _canvasTitles.Count == 0 ? null : new Dictionary<string, string>(_canvasTitles, StringComparer.Ordinal),
            _callLog,
            _type,
            _channelId,
            _forwarded,
            _isCard,
            _replyToMessageId,
            _lastSeenSeconds,
            _poll,
            _presignFinish,
            _createTimeSeconds);
        return MessageContent.FromSnapshot(snapshot);
    }

    public string BuildJson() => Build().ToJson();

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("Builder already built.");
        }
    }

    private static IReadOnlyList<T>? ToNullable<T>(List<T> values)
        => values.Count == 0 ? null : values.ToArray();

    private static IReadOnlyDictionary<string, JsonElement>? CloneExtensions(
        IReadOnlyDictionary<string, JsonElement>? extensions)
    {
        if (extensions is null || extensions.Count == 0)
        {
            return null;
        }

        var cloned = new Dictionary<string, JsonElement>(extensions.Count, StringComparer.Ordinal);
        foreach (var pair in extensions)
        {
            cloned[pair.Key] = pair.Value.Clone();
        }

        return cloned;
    }

    private static void ValidateExtensionNames(
        IReadOnlyDictionary<string, JsonElement>? extensions,
        Func<string, bool> isReserved)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var pair in extensions)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Extension names are required.", nameof(extensions));
            }

            if (isReserved(pair.Key))
            {
                throw new ArgumentException($"'{pair.Key}' is a known markdown property; use its typed builder method.", nameof(extensions));
            }
        }
    }
}
