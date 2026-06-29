using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class NotificationChannelCategorySettingResponse
    {
        [JsonProperty("action")]
        public int? Action { get; set; }

        [JsonProperty("channel_category_label")]
        public string? ChannelCategoryLabel { get; set; }

        [JsonProperty("channel_category_title")]
        public string? ChannelCategoryTitle { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }
}
