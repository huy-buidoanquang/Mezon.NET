using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ClanUsersResponse
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_users")]
        public List<ClanUserResponse>? ClanUsers { get; set; }

        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}
