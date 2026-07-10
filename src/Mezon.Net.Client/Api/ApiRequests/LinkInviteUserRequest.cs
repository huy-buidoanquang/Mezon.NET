using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class LinkInviteUserRequest
    {
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("expiry_time")]
        public int? ExpiryTime { get; set; }
    }
}
