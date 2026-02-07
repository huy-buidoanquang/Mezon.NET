using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class NotificationChannelCategorySettingsResponse
    {
        [JsonPropertyName("notification_channel_category_settings_list")]
        public List<NotificationChannelCategorySettingResponse>? NotificationChannelCategorySettingsList { get; set; }
    }
}
