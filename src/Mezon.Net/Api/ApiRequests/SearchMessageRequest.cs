using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SearchMessageRequest
    {
        [JsonPropertyName("filters")]
        public List<FilterParamRequest>? Filters { get; set; }

        [JsonPropertyName("from")]
        public int? From { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("sorts")]
        public List<SortParamRequest>? Sorts { get; set; }
    }
}
