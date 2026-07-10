using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UserActivitiesResponse
    {
        [JsonProperty("activities")]
        public List<UserActivitiesResponse>? Activities { get; set; }
    }
}
