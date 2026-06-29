using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UsersResponse
    {
        [JsonPropertyName("users")]
        public List<UserResponse>? Users { get; set; }
    }
}
