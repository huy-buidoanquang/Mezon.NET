using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UsersResponse
    {
        [JsonProperty("users")]
        public List<UserResponse>? Users { get; set; }
    }
}
