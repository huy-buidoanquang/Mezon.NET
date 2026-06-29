using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class RoleEventResponse
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }

        [JsonPropertyName("limit")]
        public string Limit { get; set; }

        [JsonPropertyName("roles")]
        public RolesResponse? Roles { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }
    }
}
