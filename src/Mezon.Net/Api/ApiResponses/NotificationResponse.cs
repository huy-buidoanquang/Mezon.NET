using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents a notification from the server.
    /// </summary>
    public class NotificationResponse
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_type")]
        public int? ChannelType { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// Category code for this notification.
        /// </summary>
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        /// <summary>
        /// Content of the notification in JSON format.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the notification was created.
        /// </summary>
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        /// <summary>
        /// The ID of the notification.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// True if this notification was persisted to the database.
        /// </summary>
        [JsonPropertyName("persistent")]
        public bool? Persistent { get; set; }

        /// <summary>
        /// The ID of the sender, if a user.
        /// </summary>
        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        /// <summary>
        /// The subject of the notification.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        /// <summary>
        /// The notification category.
        /// </summary>
        [JsonPropertyName("category")]
        public int? Category { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }

        [JsonPropertyName("channel")]
        public ChannelDescriptionResponse? Channel { get; set; }
    }
}
