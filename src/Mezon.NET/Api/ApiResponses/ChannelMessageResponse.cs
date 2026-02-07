using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelMessageResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("attachments")]
        public string Attachments { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; } = "";

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("mentions")]
        public string Mentions { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = "";

        [JsonPropertyName("reactions")]
        public string Reactions { get; set; }

        [JsonPropertyName("referenced_message")]
        public string ReferencedMessage { get; set; }

        [JsonPropertyName("references")]
        public string References { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; } = "";

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        [JsonPropertyName("hide_editted")]
        public bool? HideEditted { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }
    }
}
