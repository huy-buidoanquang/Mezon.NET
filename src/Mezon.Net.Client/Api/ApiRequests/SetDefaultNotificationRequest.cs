using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class SetDefaultNotificationRequest
    {
        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("notification_type")]
        public int? NotificationType { get; set; }
    }
}
