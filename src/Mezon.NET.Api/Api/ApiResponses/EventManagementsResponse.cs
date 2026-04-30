using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class EventManagementsResponse
    {
        [JsonProperty("events")]
        public List<EventManagementResponse>? Events { get; set; }
    }
}
