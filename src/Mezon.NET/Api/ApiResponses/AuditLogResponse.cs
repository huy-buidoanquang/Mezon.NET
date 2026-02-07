using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class AuditLogResponse
    {
        [JsonPropertyName("action_log")]
        public string ActionLog { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("details")]
        public string Details { get; set; }

        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; }

        [JsonPropertyName("entity_name")]
        public string EntityName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("time_log")]
        public string TimeLog { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
