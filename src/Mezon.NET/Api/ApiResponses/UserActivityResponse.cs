using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UserActivityResponse
    {
        [JsonPropertyName("activity_description")]
        public string ActivityDescription { get; set; }

        [JsonPropertyName("activity_name")]
        public string ActivityName { get; set; }

        [JsonPropertyName("activity_type")]
        public int? ActivityType { get; set; }

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
