using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class RolesResponse
    {
        [JsonPropertyName("cacheable_cursor")]
        public string CacheableCursor { get; set; }

        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; }

        [JsonPropertyName("prev_cursor")]
        public string PrevCursor { get; set; }

        [JsonPropertyName("roles")]
        public List<RoleResponse>? Roles { get; set; }
    }
}
