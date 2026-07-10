using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UserStatusResponse
    {
        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }
    }
}
