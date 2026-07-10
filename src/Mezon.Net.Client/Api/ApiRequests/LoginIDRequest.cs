using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class LoginIDRequest
    {
        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("platform")]
        public string? Platform { get; set; }
    }
}
