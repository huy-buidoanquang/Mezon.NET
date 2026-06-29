using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a hashtag entity within a message.
    /// </summary>
    public class HashtagOnMessage : StartEndIndex
    {
        [JsonPropertyName("channelid")]
        public string ChannelId { get; set; }
    }
}
