using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SessionRefreshRequest
    {
        [JsonPropertyName("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonPropertyName("token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }
}
