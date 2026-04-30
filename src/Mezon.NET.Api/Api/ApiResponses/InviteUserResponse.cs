using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class InviteUserResponse
    {
        [JsonProperty("channel_desc")]
        public ChannelDescriptionResponse? ChannelDesc { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_label")]
        public string? ChannelLabel { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_name")]
        public string? ClanName { get; set; }

        [JsonProperty("user_joined")]
        public bool? UserJoined { get; set; }

        [JsonProperty("expiry_time")]
        public string? ExpiryTime { get; set; }

        [JsonProperty("clan_logo")]
        public string? ClanLogo { get; set; }

        [JsonProperty("member_count")]
        public int? MemberCount { get; set; }
    }
}
