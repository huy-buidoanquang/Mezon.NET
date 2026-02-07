using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class NotificationsResponse
    {
        [JsonProperty("cacheable_cursor")]
        public string? CacheableCursor { get; set; }

        [JsonProperty("notifications")]
        public List<NotificationResponse>? Notifications { get; set; }
    }
}
