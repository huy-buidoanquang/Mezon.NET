using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    /// <summary>
    /// Represents the header of a channel message, often used for summaries.
    /// </summary>
    public class ChannelMessageHeaderResponse
    {
        [JsonProperty("attachment")]
        public string? Attachment { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("mention")]
        public string? Mention { get; set; }

        [JsonProperty("reaction")]
        public string? Reaction { get; set; }

        [JsonProperty("reference")]
        public string? Reference { get; set; }

        [JsonProperty("repliers")]
        public List<string>? Repliers { get; set; }

        [JsonProperty("sender_id")]
        public string? SenderId { get; set; }

        [JsonProperty("timestamp_seconds")]
        public long? TimestampSeconds { get; set; }
    }
}
