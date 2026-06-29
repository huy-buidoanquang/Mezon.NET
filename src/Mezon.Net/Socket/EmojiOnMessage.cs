using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an emoji entity within a message.
    /// </summary>
    public class EmojiOnMessage : StartEndIndex
    {
        [JsonPropertyName("emojiid")]
        public string EmojiId { get; set; }
    }
}
