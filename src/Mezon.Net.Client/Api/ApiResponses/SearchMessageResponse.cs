using System.Collections.Generic;
using Mezon.Net.Api.Api.ApiResponses;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class SearchMessageResponse
    {
        [JsonProperty("messages")]
        public List<SearchMessageDocumentResponse>? Messages { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }
    }
}
