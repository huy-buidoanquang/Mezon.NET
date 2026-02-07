using System.Collections.Generic;
using Mezon.NET.Api.Api.ApiResponses;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class SearchMessageResponse
    {
        [JsonProperty("messages")]
        public List<SearchMessageDocumentResponse>? Messages { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }
    }
}
