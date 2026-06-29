using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelMessagesResponse
    {
        [JsonPropertyName("last_seen_message")]
        public ChannelMessageHeaderResponse? LastSeenMessage { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ChannelMessageHeaderResponse? LastSentMessage { get; set; }

        [JsonPropertyName("messages")]
        public List<ChannelMessageResponse>? Messages { get; set; }
    }
}
