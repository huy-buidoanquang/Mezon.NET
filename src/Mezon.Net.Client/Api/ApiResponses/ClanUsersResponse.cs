using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class ClanUsersResponse
    {
        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_users")]
        public List<ClanUserResponse>? ClanUsers { get; set; }

        [JsonProperty("cursor")]
        public string? Cursor { get; set; }
    }
}
