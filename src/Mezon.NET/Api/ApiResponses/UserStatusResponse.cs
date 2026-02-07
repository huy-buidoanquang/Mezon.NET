using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UserStatusResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
