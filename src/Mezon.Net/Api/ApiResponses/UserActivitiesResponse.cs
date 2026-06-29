using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UserActivitiesResponse
    {
        [JsonPropertyName("activities")]
        public List<UserActivitiesResponse>? Activities { get; set; }
    }
}
