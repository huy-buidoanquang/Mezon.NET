using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class AppsResponse
    {
        [JsonPropertyName("apps")]
        public List<AppResponse>? Apps { get; set; }

        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; }

        [JsonPropertyName("total_count")]
        public int? TotalCount { get; set; }
    }
}
