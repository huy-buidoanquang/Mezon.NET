using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class FriendResponse
    {
        [JsonProperty("state")]
        public int? State { get; set; }

        [JsonProperty("update_time")]
        public string? UpdateTime { get; set; }

        [JsonProperty("user")]
        public UserResponse? User { get; set; }
    }
}
