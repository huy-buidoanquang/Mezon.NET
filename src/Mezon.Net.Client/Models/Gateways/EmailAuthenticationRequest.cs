using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class EmailAuthenticationRequest
    {
        [JsonProperty("account")]
        public AccountEmailRequest? Account { get; set; }

        [JsonProperty("create")]
        public bool? Create { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
