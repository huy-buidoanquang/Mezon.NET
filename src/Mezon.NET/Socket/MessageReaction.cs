using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a reaction to a message.
    /// </summary>
    public class MessageReaction
    {
        [JsonPropertyName("action")]
        public bool? Action { get; set; }

        [JsonPropertyName("emoji_id")]
        public string EmojiId { get; set; }

        [JsonPropertyName("emoji")]
        public string Emoji { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("sender_name")]
        public string SenderName { get; set; }

        [JsonPropertyName("sender_avatar")]
        public string SenderAvatar { get; set; }

        /// <summary>
        /// The total count of this emoji reaction.
        /// </summary>
        [JsonPropertyName("count")]
        public int? Count { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        /// <summary>
        /// The channel label.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The message that the user reacted to.
        /// </summary>
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }
}
