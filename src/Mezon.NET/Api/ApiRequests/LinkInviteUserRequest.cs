using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class LinkInviteUserRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("expiry_time")]
        public int? ExpiryTime { get; set; }
    }
}
