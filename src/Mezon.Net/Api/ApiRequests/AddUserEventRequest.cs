using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class AddUserEventRequest
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }
    }
}
