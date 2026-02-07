using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a file attachment in a message.
    /// </summary>
    public class MessageAttachment
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("filetype")]
        public string Filetype { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

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
        /// The message that contains the attachment.
        /// </summary>
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        /// <summary>
        /// Message sender, usually a user ID.
        /// </summary>
        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }
    }
}
