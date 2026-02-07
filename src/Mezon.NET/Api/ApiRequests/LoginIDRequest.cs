using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class LoginIDRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }
    }
}
