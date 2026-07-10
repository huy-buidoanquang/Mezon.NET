using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class ClanNotificationSettingResponse
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }
}
