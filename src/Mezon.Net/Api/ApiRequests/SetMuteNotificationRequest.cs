using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SetMuteNotificationRequest
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }
    }
}
