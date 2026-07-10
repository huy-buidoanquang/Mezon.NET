using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Represents a notification from the server.
    /// </summary>
    public class NotificationResponse
    {
        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_type")]
        public int? ChannelType { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        /// <summary>
        /// Category code for this notification.
        /// </summary>
        [JsonProperty("code")]
        public int? Code { get; set; }

        /// <summary>
        /// Content of the notification in JSON format.
        /// </summary>
        [JsonProperty("content")]
        public string? Content { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the notification was created.
        /// </summary>
        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        /// <summary>
        /// The ID of the notification.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// True if this notification was persisted to the database.
        /// </summary>
        [JsonProperty("persistent")]
        public bool? Persistent { get; set; }

        /// <summary>
        /// The ID of the sender, if a user.
        /// </summary>
        [JsonProperty("sender_id")]
        public string? SenderId { get; set; }

        /// <summary>
        /// The subject of the notification.
        /// </summary>
        [JsonProperty("subject")]
        public string? Subject { get; set; }

        /// <summary>
        /// The notification category.
        /// </summary>
        [JsonProperty("category")]
        public int? Category { get; set; }

        [JsonProperty("topic_id")]
        public string? TopicId { get; set; }

        [JsonProperty("channel")]
        public ChannelDescriptionResponse? Channel { get; set; }
    }
}
