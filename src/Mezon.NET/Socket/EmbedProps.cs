using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an embed object, typically for rich content display in messages.
    /// </summary>
    public class EmbedProps
    {
        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("author")]
        public EmbedAuthor? Author { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("thumbnail")]
        public EmbedImage? Thumbnail { get; set; }

        [JsonPropertyName("fields")]
        public List<EmbedField>? Fields { get; set; }

        [JsonPropertyName("image")]
        public EmbedImage? Image { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("footer")]
        public EmbedFooter? Footer { get; set; }
    }

    /// <summary>
    /// Represents the author of an embed.
    /// </summary>
    public class EmbedAuthor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    /// <summary>
    /// Represents a field in an embed, with a name and a value.
    /// </summary>
    public class EmbedField
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("inline")]
        public bool? Inline { get; set; }
    }

    /// <summary>
    /// Represents an image or thumbnail in an embed.
    /// </summary>
    public class EmbedImage
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    /// <summary>
    /// Represents the footer of an embed.
    /// </summary>
    public class EmbedFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }
}
