using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AuthenticateSMSRequest
    {
        [JsonProperty("account")]
        public AccountSMSRequest? AccountSMSRequest { get; set; }

        [JsonProperty("create")]
        public bool? Create { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
