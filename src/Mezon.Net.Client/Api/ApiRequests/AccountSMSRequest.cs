using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class AccountSMSRequest
    {
        [JsonProperty("phoneno")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonProperty("vars")]
        public IDictionary<string, string>? Vars { get; set; }
    }
}
