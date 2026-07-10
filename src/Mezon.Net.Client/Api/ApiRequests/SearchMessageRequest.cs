using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class SearchMessageRequest
    {
        [JsonProperty("filters")]
        public List<FilterParamRequest>? Filters { get; set; }

        [JsonProperty("from")]
        public int? From { get; set; }

        [JsonProperty("size")]
        public int? Size { get; set; }

        [JsonProperty("sorts")]
        public List<SortParamRequest>? Sorts { get; set; }
    }
}
