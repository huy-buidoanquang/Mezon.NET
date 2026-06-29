using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UserPermissionsInChannelResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("permissions")]
        public PermissionsResponse? Permissions { get; set; }
    }
}
