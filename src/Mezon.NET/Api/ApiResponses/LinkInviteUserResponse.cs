using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class LinkInviteUserResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("expiry_time")]
        public string ExpiryTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("invite_link")]
        public string InviteLink { get; set; }
    }
}
