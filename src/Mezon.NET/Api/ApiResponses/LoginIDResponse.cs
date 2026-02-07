using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class LoginIDResponse
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("create_time_second")]
        public string CreateTimeSecond { get; set; }

        [JsonPropertyName("login_id")]
        public string LoginId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
