using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class CreateActivityRequest
    {
        [JsonProperty("activity_description")]
        public string? ActivityDescription { get; set; }

        [JsonProperty("activity_name")]
        public string? ActivityName { get; set; }

        [JsonProperty("activity_type")]
        public int? ActivityType { get; set; }

        [JsonProperty("application_id")]
        public string? ApplicationId { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("status")]
        public int? Status { get; set; }
    }
}
