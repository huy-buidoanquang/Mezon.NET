using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class SetMuteNotificationRequest
    {
        [JsonProperty("active")]
        public int? Active { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("notification_type")]
        public int? NotificationType { get; set; }
    }
}
