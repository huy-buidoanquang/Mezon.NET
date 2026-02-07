using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the rich content of a channel message.
    /// </summary>
    public class ChannelMessageContent
    {
        /// <summary>
        /// The primary text content of the message.
        /// </summary>
        [JsonPropertyName("t")]
        public string Text { get; set; }

        /// <summary>
        /// The content of the thread this message is part of.
        /// </summary>
        [JsonPropertyName("contentThread")]
        public string ContentThread { get; set; }

        /// <summary>
        /// A list of hashtags present in the message.
        /// </summary>
        [JsonPropertyName("hg")]
        public List<HashtagOnMessage>? Hashtags { get; set; }

        /// <summary>
        /// A list of emojis present in the message.
        /// </summary>
        [JsonPropertyName("ej")]
        public List<EmojiOnMessage>? Emojis { get; set; }

        /// <summary>
        /// A list of hyperlinks present in the message.
        /// </summary>
        [JsonPropertyName("lk")]
        public List<LinkOnMessage>? Links { get; set; }

        /// <summary>
        /// A list of markdown elements present in the message.
        /// </summary>
        [JsonPropertyName("mk")]
        public List<MarkdownOnMessage>? Markdown { get; set; }

        /// <summary>
        /// A list of voice room links present in the message.
        /// </summary>
        [JsonPropertyName("vk")]
        public List<LinkVoiceRoomOnMessage>? VoiceRoomLinks { get; set; }

        /// <summary>
        /// A list of embedded content items.
        /// </summary>
        [JsonPropertyName("embed")]
        public List<EmbedProps>? Embeds { get; set; }

        /// <summary>
        /// A list of interactive components, like buttons or select menus.
        /// The type is 'object' to accommodate various complex structures.
        /// </summary>
        [JsonPropertyName("components")]
        public object? Components { get; set; }
    }

    /// <summary>
    /// Represents the start and end index of an entity within a message string.
    /// </summary>
    public class StartEndIndex
    {
        /// <summary>
        /// The starting index (inclusive).
        /// </summary>
        [JsonPropertyName("s")]
        public int? S { get; set; }

        /// <summary>
        /// The ending index (exclusive).
        /// </summary>
        [JsonPropertyName("e")]
        public int? E { get; set; }
    }
}
