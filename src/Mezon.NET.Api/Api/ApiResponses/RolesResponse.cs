using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class RolesResponse
    {
        [JsonProperty("cacheable_cursor")]
        public string? CacheableCursor { get; set; }

        [JsonProperty("next_cursor")]
        public string? NextCursor { get; set; }

        [JsonProperty("prev_cursor")]
        public string? PrevCursor { get; set; }

        [JsonProperty("roles")]
        public List<RoleResponse>? Roles { get; set; }
    }
}
