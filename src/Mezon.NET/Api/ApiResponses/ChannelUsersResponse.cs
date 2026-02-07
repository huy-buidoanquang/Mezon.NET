using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelUsersResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_users")]
        public List<ChannelUserResponse>? ChannelUsers { get; set; }

        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}
