using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class FriendsResponse
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }

        [JsonPropertyName("friends")]
        public List<FriendResponse>? Friends { get; set; }
    }
}
