using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AddUserEventRequest
    {
        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("event_id")]
        public string? EventId { get; set; }
    }
}
