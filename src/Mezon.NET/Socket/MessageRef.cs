using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a reference to another message, often used for replies.
    /// </summary>
    public class MessageRef
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("message_ref_id")]
        public string MessageRefId { get; set; }

        [JsonPropertyName("ref_type")]
        public int? RefType { get; set; }

        [JsonPropertyName("message_sender_id")]
        public string MessageSenderId { get; set; }

        /// <summary>
        /// The original message sender's username.
        /// </summary>
        [JsonPropertyName("message_sender_username")]
        public string MessageSenderUsername { get; set; }

        /// <summary>
        /// The original message sender's avatar.
        /// Note: The C# property name 'MessageSenderAvatar' corrects a typo in the original 'mesages_sender_avatar'.
        /// </summary>
        [JsonPropertyName("mesages_sender_avatar")]
        public string MessageSenderAvatar { get; set; }

        /// <summary>
        /// The original sender's clan nickname.
        /// </summary>
        [JsonPropertyName("message_sender_clan_nick")]
        public string MessageSenderClanNick { get; set; }

        /// <summary>
        /// The original sender's display name.
        /// </summary>
        [JsonPropertyName("message_sender_display_name")]
        public string MessageSenderDisplayName { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("has_attachment")]
        public bool? HasAttachment { get; set; }

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
    }
}
