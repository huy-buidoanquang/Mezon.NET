using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class GenerateMezonMeetResponse
    {
        [JsonPropertyName("meet_id")]
        public string MeetId { get; set; }
        [JsonPropertyName("room_name")]
        public string RoomName { get; set; }
        [JsonPropertyName("external_link")]
        public string ExternalLink { get; set; }
        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }
    }
}
