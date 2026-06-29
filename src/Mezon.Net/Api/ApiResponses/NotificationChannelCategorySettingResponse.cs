using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class NotificationChannelCategorySettingResponse
    {
        [JsonPropertyName("action")]
        public int? Action { get; set; }

        [JsonPropertyName("channel_category_label")]
        public string ChannelCategoryLabel { get; set; }

        [JsonPropertyName("channel_category_title")]
        public string ChannelCategoryTitle { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }
}
