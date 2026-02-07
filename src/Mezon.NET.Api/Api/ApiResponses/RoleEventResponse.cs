using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class RoleEventResponse
    {
        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("cursor")]
        public string? Cursor { get; set; }

        [JsonProperty("limit")]
        public string? Limit { get; set; }

        [JsonProperty("roles")]
        public RolesResponse? Roles { get; set; }

        [JsonProperty("state")]
        public string? State { get; set; }
    }
}
