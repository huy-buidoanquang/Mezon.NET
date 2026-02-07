using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ChannelMessageResponse
    {
        [JsonProperty("id")]
        public string? Id { get; set; } = "";

        [JsonProperty("attachments")]
        public string? Attachments { get; set; }

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }

        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; } = "";

        [JsonProperty("channel_label")]
        public string? ChannelLabel { get; set; } = "";

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_logo")]
        public string? ClanLogo { get; set; }

        [JsonProperty("clan_nick")]
        public string? ClanNick { get; set; }

        [JsonProperty("clan_avatar")]
        public string? ClanAvatar { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; } = "";

        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        [JsonProperty("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("mentions")]
        public string? Mentions { get; set; }

        [JsonProperty("message_id")]
        public string? MessageId { get; set; } = "";

        [JsonProperty("reactions")]
        public string? Reactions { get; set; }

        [JsonProperty("referenced_message")]
        public string? ReferencedMessage { get; set; }

        [JsonProperty("references")]
        public string? References { get; set; }

        [JsonProperty("sender_id")]
        public string? SenderId { get; set; } = "";

        [JsonProperty("update_time")]
        public string? UpdateTime { get; set; }

        [JsonProperty("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("mode")]
        public int? Mode { get; set; }

        [JsonProperty("hide_editted")]
        public bool? HideEditted { get; set; }

        [JsonProperty("topic_id")]
        public string? TopicId { get; set; }
    }
}
