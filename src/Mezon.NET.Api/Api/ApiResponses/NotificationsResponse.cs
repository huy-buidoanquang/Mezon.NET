using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class NotificationsResponse
    {
        [JsonProperty("cacheable_cursor")]
        public string? CacheableCursor { get; set; }

        [JsonProperty("notifications")]
        public List<NotificationResponse>? Notifications { get; set; }
    }
}
