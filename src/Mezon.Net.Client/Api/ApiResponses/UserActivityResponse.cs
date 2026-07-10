using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UserActivityResponse
    {
        [JsonProperty("activity_description")]
        public string? ActivityDescription { get; set; }

        [JsonProperty("activity_name")]
        public string? ActivityName { get; set; }

        [JsonProperty("activity_type")]
        public int? ActivityType { get; set; }

        [JsonProperty("application_id")]
        public string? ApplicationId { get; set; }

        [JsonProperty("end_time")]
        public string? EndTime { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("status")]
        public int? Status { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }
    }
}
