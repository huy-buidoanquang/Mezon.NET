using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class ChannelUsersResponse
    {
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_users")]
        public List<ChannelUserResponse>? ChannelUsers { get; set; }

        [JsonProperty("cursor")]
        public string? Cursor { get; set; }
    }
}
