using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class SetNotificationChannelRequest
    {
        [JsonProperty("channel_category_id")]
        public string? ChannelCategoryId { get; set; }

        [JsonProperty("notification_type")]
        public int? NotificationType { get; set; }

        [JsonProperty("time_mute")]
        public string? TimeMute { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }
    }
}
