using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AccountEmailRequest
    {
        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("password")]
        public string? Password { get; set; }

        [JsonProperty("prev_email")]
        public string? PrevEmail { get; set; }

        [JsonProperty("vars")]
        public IDictionary<string, string>? Vars { get; set; }
    }
}
