using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UpdateEventRequest
    {
        [JsonProperty("event_id")]
        public string? EventId { get; set; }

        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

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

        [JsonProperty("logo")]
        public string? Logo { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("channel_id_old")]
        public string? ChannelIdOld { get; set; }

        [JsonProperty("repeat_type")]
        public int? RepeatType { get; set; }
    }
}
