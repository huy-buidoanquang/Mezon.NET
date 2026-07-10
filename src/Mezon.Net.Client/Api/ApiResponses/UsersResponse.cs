using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UsersResponse
    {
        [JsonProperty("users")]
        public List<UserResponse>? Users { get; set; }
    }
}
