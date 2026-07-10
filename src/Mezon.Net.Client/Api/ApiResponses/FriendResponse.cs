using Newtonsoft.Json;

namespace Mezon.Net.Client
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
