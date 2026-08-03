using System.Collections.Generic;
using System.Text.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Rich embed inside content <c>embed</c>. Unknown embed fields are kept in <see cref="Extensions"/>.
    /// </summary>
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

    public sealed class MessageEmbedAuthor
    {
        public MessageEmbedAuthor(string name, string? iconUrl = null, string? url = null)
        {
            Name = name;
            IconUrl = iconUrl;
            Url = url;
        }

        public string Name { get; }
        public string? IconUrl { get; }
        public string? Url { get; }
    }

    public sealed class MessageEmbedThumbnail
    {
        public MessageEmbedThumbnail(string url) => Url = url;
        public string Url { get; }
    }

    public sealed class MessageEmbedImage
    {
        public MessageEmbedImage(string url, string? width = null, string? height = null)
        {
            Url = url;
            Width = width;
            Height = height;
        }

        public string Url { get; }
        public string? Width { get; }
        public string? Height { get; }
    }

    public sealed class MessageEmbedFooter
    {
        public MessageEmbedFooter(string text, string? iconUrl = null)
        {
            Text = text;
            IconUrl = iconUrl;
        }

        public string Text { get; }
        public string? IconUrl { get; }
    }

    public sealed class MessageEmbedField
    {
        public MessageEmbedField(string name, string value, bool inline = false, JsonElement? inputs = null, JsonElement? options = null, int? maxOptions = null)
        {
            Name = name;
            Value = value;
            Inline = inline;
            Inputs = inputs;
            Options = options;
            MaxOptions = maxOptions;
        }

        public string Name { get; }
        public string Value { get; }
        public bool Inline { get; }
        public JsonElement? Inputs { get; }
        public JsonElement? Options { get; }
        public int? MaxOptions { get; }
    }
}
