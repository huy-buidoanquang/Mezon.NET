using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AppsResponse
    {
        [JsonProperty("apps")]
        public List<AppResponse>? Apps { get; set; }

        [JsonProperty("next_cursor")]
        public string? NextCursor { get; set; }

        [JsonProperty("total_count")]
        public int? TotalCount { get; set; }
    }
}
