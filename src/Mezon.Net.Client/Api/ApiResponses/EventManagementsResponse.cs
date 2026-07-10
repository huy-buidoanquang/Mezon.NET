using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class EventManagementsResponse
    {
        [JsonProperty("events")]
        public List<EventManagementResponse>? Events { get; set; }
    }
}
