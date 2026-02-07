using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateEventUserRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("event_label")]
        public string EventLabel { get; set; }
    }
}
