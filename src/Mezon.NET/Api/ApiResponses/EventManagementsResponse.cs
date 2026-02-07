using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class EventManagementsResponse
    {
        [JsonPropertyName("events")]
        public List<EventManagementResponse>? Events { get; set; }
    }
}
