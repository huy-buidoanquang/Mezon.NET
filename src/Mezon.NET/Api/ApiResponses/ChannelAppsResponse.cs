using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelAppsResponse
    {
        [JsonPropertyName("channel_apps")]
        public List<ChannelAppResponse>? ChannelApps { get; set; }
    }
}
