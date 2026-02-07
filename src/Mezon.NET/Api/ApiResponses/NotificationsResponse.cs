using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class NotificationsResponse
    {
        [JsonPropertyName("cacheable_cursor")]
        public string CacheableCursor { get; set; }

        [JsonPropertyName("notifications")]
        public List<NotificationResponse>? Notifications { get; set; }
    }
}
