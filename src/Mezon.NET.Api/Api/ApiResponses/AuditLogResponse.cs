using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class AuditLogResponse
    {
        [JsonProperty("action_log")]
        public string? ActionLog { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_label")]
        public string? ChannelLabel { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("details")]
        public string? Details { get; set; }

        [JsonProperty("entity_id")]
        public string? EntityId { get; set; }

        [JsonProperty("entity_name")]
        public string? EntityName { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("time_log")]
        public string? TimeLog { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }
    }
}
