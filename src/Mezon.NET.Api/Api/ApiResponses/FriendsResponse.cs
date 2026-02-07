using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class FriendsResponse
    {
        [JsonProperty("cursor")]
        public string? Cursor { get; set; }

        [JsonProperty("friends")]
        public List<FriendResponse>? Friends { get; set; }
    }
}
