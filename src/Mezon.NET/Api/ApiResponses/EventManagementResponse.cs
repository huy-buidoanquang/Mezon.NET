using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class EventManagementResponse
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_voice_id")]
        public string ChannelVoiceId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("max_permission")]
        public int? MaxPermission { get; set; }

        [JsonPropertyName("start_event")]
        public int? StartEvent { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("event_status")]
        public int? EventStatus { get; set; }

        [JsonPropertyName("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonPropertyName("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonPropertyName("meet_room")]
        public GenerateMezonMeetResponse? MeetRoom { get; set; }
    }
}
