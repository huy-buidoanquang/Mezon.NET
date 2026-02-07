using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ChannelMessagesResponse
    {
        [JsonProperty("last_seen_message")]
        public ChannelMessageHeaderResponse? LastSeenMessage { get; set; }

        [JsonProperty("last_sent_message")]
        public ChannelMessageHeaderResponse? LastSentMessage { get; set; }

        [JsonProperty("messages")]
        public List<ChannelMessageResponse>? Messages { get; set; }
    }
}
