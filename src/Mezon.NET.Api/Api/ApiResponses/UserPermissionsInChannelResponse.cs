using Newtonsoft.Json;

namespace Mezon.NET.Api
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
