using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class SearchMessageResponse
    {
        [JsonProperty("messages")]
        public List<SearchMessageDocumentResponse>? Messages { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }
    }
}
