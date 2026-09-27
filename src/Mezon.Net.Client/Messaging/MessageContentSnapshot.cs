using System.Collections.Generic;
using System.Text.Json;

namespace Mezon.Net.Client;

/// <summary>Lazy-materialized typed fields for a content payload.</summary>
internal readonly struct MessageContentSnapshot
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
        IReadOnlyDictionary<string, JsonElement>? unknown)
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
}
