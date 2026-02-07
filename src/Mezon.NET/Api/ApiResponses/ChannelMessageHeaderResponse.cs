using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents the header of a channel message, often used for summaries.
    /// </summary>
    public class ChannelMessageHeaderResponse
    {
        [JsonPropertyName("attachment")]
        public string Attachment { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("mention")]
        public string Mention { get; set; }

        [JsonPropertyName("reaction")]
        public string Reaction { get; set; }

        [JsonPropertyName("reference")]
        public string Reference { get; set; }

        [JsonPropertyName("repliers")]
        public List<string>? Repliers { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("timestamp_seconds")]
        public long? TimestampSeconds { get; set; }
    }
}
