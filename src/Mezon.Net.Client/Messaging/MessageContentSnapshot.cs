using System.Collections.Generic;
using System.Text.Json;

namespace Mezon.Net.Client;

/// <summary>
/// Lazy-materialized typed fields for a content payload. A class (not a struct) so it is published to concurrent
/// readers with one atomic reference write.
/// </summary>
internal sealed class MessageContentSnapshot
{
    public MessageContentSnapshot(
        string? text,
        IReadOnlyList<HashtagOnMessage>? hashtags,
        IReadOnlyList<EmojiOnMessage>? emojis,
        IReadOnlyList<LinkOnMessage>? links,
        IReadOnlyList<MarkdownOnMessage>? markdown,
        IReadOnlyList<LinkVoiceRoomOnMessage>? voiceLinks,
        IReadOnlyList<MessageEmbed>? embeds,
        IReadOnlyList<MessageActionRow>? components,
        IReadOnlyDictionary<string, JsonElement>? unknown,
        IReadOnlyList<PreOnMessage>? pre = null,
        IReadOnlyList<BoldOnMessage>? bold = null,
        IReadOnlyList<LinkYoutubeOnMessage>? youtubeLinks = null,
        int? e2ee = null,
        JsonElement? canvas = null,
        IReadOnlyDictionary<string, string>? canvasTitles = null,
        MessageCallLog? callLog = null,
        string? type = null,
        string? channelId = null,
        bool? forwarded = null,
        bool? isCard = null,
        long? replyToMessageId = null,
        long? lastSeenSeconds = null,
        MessagePoll? poll = null,
        IReadOnlyList<string>? presignFinish = null,
        long? createTimeSeconds = null)
    {
        Text = text;
        Hashtags = hashtags;
        Emojis = emojis;
        Links = links;
        Markdown = markdown;
        VoiceLinks = voiceLinks;
        Embeds = embeds;
        Components = components;
        Unknown = unknown;
        Pre = pre;
        Bold = bold;
        YoutubeLinks = youtubeLinks;
        E2ee = e2ee;
        Canvas = canvas;
        CanvasTitles = canvasTitles;
        CallLog = callLog;
        Type = type;
        ChannelId = channelId;
        Forwarded = forwarded;
        IsCard = isCard;
        ReplyToMessageId = replyToMessageId;
        LastSeenSeconds = lastSeenSeconds;
        Poll = poll;
        PresignFinish = presignFinish;
        CreateTimeSeconds = createTimeSeconds;
    }

    public string? Text { get; }
    public IReadOnlyList<HashtagOnMessage>? Hashtags { get; }
    public IReadOnlyList<EmojiOnMessage>? Emojis { get; }
    public IReadOnlyList<LinkOnMessage>? Links { get; }
    public IReadOnlyList<MarkdownOnMessage>? Markdown { get; }
    public IReadOnlyList<LinkVoiceRoomOnMessage>? VoiceLinks { get; }
    public IReadOnlyList<MessageEmbed>? Embeds { get; }
    public IReadOnlyList<MessageActionRow>? Components { get; }
    public IReadOnlyDictionary<string, JsonElement>? Unknown { get; }
    public IReadOnlyList<PreOnMessage>? Pre { get; }
    public IReadOnlyList<BoldOnMessage>? Bold { get; }
    public IReadOnlyList<LinkYoutubeOnMessage>? YoutubeLinks { get; }
    public int? E2ee { get; }
    public JsonElement? Canvas { get; }
    public IReadOnlyDictionary<string, string>? CanvasTitles { get; }
    public MessageCallLog? CallLog { get; }
    public string? Type { get; }
    public string? ChannelId { get; }
    public bool? Forwarded { get; }
    public bool? IsCard { get; }
    public long? ReplyToMessageId { get; }
    public long? LastSeenSeconds { get; }
    public MessagePoll? Poll { get; }
    public IReadOnlyList<string>? PresignFinish { get; }
    public long? CreateTimeSeconds { get; }
}
