using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UserActivitiesResponse
    {
        [JsonProperty("activities")]
        public List<UserActivitiesResponse>? Activities { get; set; }
    }
}
