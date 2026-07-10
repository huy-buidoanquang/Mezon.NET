using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class LinkInviteUserResponse
    {
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("expiry_time")]
        public string? ExpiryTime { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("invite_link")]
        public string? InviteLink { get; set; }
    }
}
