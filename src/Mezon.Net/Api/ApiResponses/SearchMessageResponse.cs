using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class SearchMessageResponse
    {
        [JsonPropertyName("messages")]
        public List<ApiSearchMessageDocument>? Messages { get; set; }

        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }
}
