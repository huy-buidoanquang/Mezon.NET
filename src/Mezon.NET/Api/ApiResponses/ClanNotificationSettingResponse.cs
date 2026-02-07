using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ClanNotificationSettingResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }
}
