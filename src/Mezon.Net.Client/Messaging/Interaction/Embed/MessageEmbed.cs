using System.Collections.Generic;
using System.Text.Json;

namespace Mezon.Net.Client;

/// <summary>Rich embed inside message content.</summary>
public sealed class MessageEmbed
{
    public MessageEmbed(
        string? color = null,
        string? title = null,
        string? url = null,
        MessageEmbedAuthor? author = null,
        string? description = null,
        MessageEmbedThumbnail? thumbnail = null,
        IReadOnlyList<MessageEmbedField>? fields = null,
        MessageEmbedImage? image = null,
        string? timestamp = null,
        MessageEmbedFooter? footer = null,
        IReadOnlyDictionary<string, JsonElement>? extensions = null)
    {
        Color = color;
        Title = title;
        Url = url;
        Author = author;
        Description = description;
        Thumbnail = thumbnail;
        Fields = fields;
        Image = image;
        Timestamp = timestamp;
        Footer = footer;
        Extensions = extensions;
    }

    public string? Color { get; }
    public string? Title { get; }
    public string? Url { get; }
    public MessageEmbedAuthor? Author { get; }
    public string? Description { get; }
    public MessageEmbedThumbnail? Thumbnail { get; }
    public IReadOnlyList<MessageEmbedField>? Fields { get; }
    public MessageEmbedImage? Image { get; }
    public string? Timestamp { get; }
    public MessageEmbedFooter? Footer { get; }
    public IReadOnlyDictionary<string, JsonElement>? Extensions { get; }
}
