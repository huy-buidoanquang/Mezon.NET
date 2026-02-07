using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class FriendResponse
    {
        [JsonPropertyName("state")]
        public int? State { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("user")]
        public UserResponse? User { get; set; }
    }
}
