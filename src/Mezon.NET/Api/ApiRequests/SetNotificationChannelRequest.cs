using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SetNotificationChannelRequest
    {
        [JsonPropertyName("channel_category_id")]
        public string ChannelCategoryId { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }

        [JsonPropertyName("time_mute")]
        public string TimeMute { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }
}
