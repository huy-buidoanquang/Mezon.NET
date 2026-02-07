using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class AccountMezonRequest
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("vars")]
        public IDictionary<string, string>? Vars { get; set; }
    }
}
