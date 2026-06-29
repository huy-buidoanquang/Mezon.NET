using System.Text.Json.Serialization;
using Mezon.NET.Api.ApiResponses;

namespace Mezon.NET.Api.ApiRequests
{
    public class CreateEventRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_voice_id")]
        public string ChannelVoiceId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("action")]
        public int? Action { get; set; }

        [JsonPropertyName("event_status")]
        public int? EventStatus { get; set; }

        [JsonPropertyName("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonPropertyName("creator_id")]
        public int? CreatorIdNum { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonPropertyName("meet_room")]
        public GenerateMezonMeetResponse? MeetRoom { get; set; }
    }
}
