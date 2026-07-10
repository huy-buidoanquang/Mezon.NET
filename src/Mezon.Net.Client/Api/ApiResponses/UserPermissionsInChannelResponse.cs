using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UserPermissionsInChannelResponse
    {
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("permissions")]
        public PermissionsResponse? Permissions { get; set; }
    }
}
