using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class EventManagementResponse
    {
        [JsonProperty("active")]
        public int? Active { get; set; }

        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("channel_voice_id")]
        public string? ChannelVoiceId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("end_time")]
        public string? EndTime { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("logo")]
        public string? Logo { get; set; }

        [JsonProperty("max_permission")]
        public int? MaxPermission { get; set; }

        [JsonProperty("start_event")]
        public int? StartEvent { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("user_ids")]
        public List<string>? UserIds { get; set; }

        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("event_status")]
        public int? EventStatus { get; set; }

        [JsonProperty("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonProperty("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonProperty("meet_room")]
        public GenerateMezonMeetResponse? MeetRoom { get; set; }
    }
}
