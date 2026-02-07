using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UpdateEventUserRequest
    {
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("event_id")]
        public string? EventId { get; set; }

        [JsonProperty("event_label")]
        public string? EventLabel { get; set; }
    }
}
