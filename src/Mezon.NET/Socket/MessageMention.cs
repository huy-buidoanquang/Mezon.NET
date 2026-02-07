using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a mention (user or role) within a message.
    /// </summary>
    public class MessageMention : StartEndIndex
    {
        /// <summary>
        /// The ISO string or UNIX time when the message was created.
        /// </summary>
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        /// <summary>
        /// The ID of the mentioned role.
        /// </summary>
        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }

        /// <summary>
        /// The name of the mentioned role.
        /// </summary>
        [JsonPropertyName("rolename")]
        public string RoleName { get; set; }

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
        /// The message that contains the mention.
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
