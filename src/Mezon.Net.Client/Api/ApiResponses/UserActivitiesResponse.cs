using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class UserActivitiesResponse
    {
        [JsonProperty("activities")]
        public List<UserActivitiesResponse>? Activities { get; set; }
    }
}
