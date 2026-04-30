using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class UsersResponse
    {
        [JsonProperty("users")]
        public List<UserResponse>? Users { get; set; }
    }
}
