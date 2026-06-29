using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class ChannelAppsResponse
    {
        [JsonProperty("channel_apps")]
        public List<ChannelAppResponse>? ChannelApps { get; set; }
    }
}
