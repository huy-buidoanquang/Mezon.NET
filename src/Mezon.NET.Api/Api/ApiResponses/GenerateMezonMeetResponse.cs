using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class GenerateMezonMeetResponse
    {
        [JsonProperty("meet_id")]
        public string? MeetId { get; set; }
        [JsonProperty("room_name")]
        public string? RoomName { get; set; }
        [JsonProperty("external_link")]
        public string? ExternalLink { get; set; }
        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }
        [JsonProperty("event_id")]
        public string? EventId { get; set; }
    }
}
