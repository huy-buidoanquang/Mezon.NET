using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AccountMezonRequest
    {
        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("vars")]
        public IDictionary<string, string>? Vars { get; set; }
    }
}
