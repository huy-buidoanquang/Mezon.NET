using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class InviteUserResponse
    {
        [JsonPropertyName("channel_desc")]
        public ApiChannelDescription? ChannelDesc { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("user_joined")]
        public bool? UserJoined { get; set; }

        [JsonPropertyName("expiry_time")]
        public string ExpiryTime { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("member_count")]
        public int? MemberCount { get; set; }
    }
}
