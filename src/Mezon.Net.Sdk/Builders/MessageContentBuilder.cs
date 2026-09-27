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
/// (<c>hg</c>, <c>ej</c>, <c>lk</c>, <c>mk</c>, <c>vk</c>), embeds, action rows and
/// forward-compatible unknown root properties. It delegates final serialization to
/// the same codec used by <see cref="MessageContent.Parse(string)"/>.
/// </remarks>
public sealed class MessageContentBuilder
{
    private readonly List<HashtagOnMessage> _hashtags = new();
    private readonly List<EmojiOnMessage> _emojis = new();
    private readonly List<LinkOnMessage> _links = new();
    private readonly List<MarkdownOnMessage> _markdown = new();
    private readonly List<LinkVoiceRoomOnMessage> _voiceLinks = new();
    private readonly List<MessageEmbed> _embeds = new();
    private readonly List<MessageActionRow> _components = new();
    private readonly Dictionary<string, JsonElement> _unknown = new(StringComparer.Ordinal);
    private bool _built;
    private string? _text;

    public MessageContentBuilder SetText(string? text)
    {
        EnsureMutable();
        _text = text;
        return this;
    }

    public MessageContentBuilder AddHashtag(string? channelId, int? start = null, int? end = null)
    {
        EnsureMutable();
        _hashtags.Add(new HashtagOnMessage(channelId, start, end));
        return this;
    }

    public MessageContentBuilder AddEmoji(string? emojiId, int? start = null, int? end = null)
    {
        EnsureMutable();
        _emojis.Add(new EmojiOnMessage(emojiId, start, end));
        return this;
    }

    public MessageContentBuilder AddLink(int? start = null, int? end = null)
    {
        EnsureMutable();
        _links.Add(new LinkOnMessage(start, end));
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
        _markdown.Add(new MarkdownOnMessage(type, start, end, url, language, CloneExtensions(extensions)));
        return this;
    }

    public MessageContentBuilder AddVoiceLink(int? start = null, int? end = null)
    {
        EnsureMutable();
        _voiceLinks.Add(new LinkVoiceRoomOnMessage(start, end));
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
            _unknown.Count == 0 ? null : new Dictionary<string, JsonElement>(_unknown, StringComparer.Ordinal));
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
}
