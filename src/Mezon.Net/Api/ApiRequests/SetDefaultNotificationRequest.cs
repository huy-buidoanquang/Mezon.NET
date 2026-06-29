using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SetDefaultNotificationRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }
    }
}
