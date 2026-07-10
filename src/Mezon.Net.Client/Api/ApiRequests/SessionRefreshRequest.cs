using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class SessionRefreshRequest
    {
        [JsonProperty("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }
}
