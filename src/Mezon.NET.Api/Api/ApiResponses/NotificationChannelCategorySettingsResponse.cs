using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class NotificationChannelCategorySettingsResponse
    {
        [JsonProperty("notification_channel_category_settings_list")]
        public List<NotificationChannelCategorySettingResponse>? NotificationChannelCategorySettingsList { get; set; }
    }
}
