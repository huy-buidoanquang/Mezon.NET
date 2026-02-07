using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class EmailAuthenticationRequest
    {
        [JsonPropertyName("account")]
        public AccountEmailRequest? Account { get; set; }

        [JsonPropertyName("create")]
        public bool? Create { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
