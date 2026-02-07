using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class CreateEventRequest
    {
        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("channel_voice_id")]
        public string? ChannelVoiceId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("end_time")]
        public string? EndTime { get; set; }

        [JsonProperty("logo")]
        public string? Logo { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("action")]
        public int? Action { get; set; }

        [JsonProperty("event_status")]
        public int? EventStatus { get; set; }

        [JsonProperty("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonProperty("creator_id")]
        public int? CreatorIdNum { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }

        [JsonProperty("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonProperty("meet_room")]
        public GenerateMezonMeetResponse? MeetRoom { get; set; }
    }
}
