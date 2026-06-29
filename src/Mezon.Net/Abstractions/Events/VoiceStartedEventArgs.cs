using System.Text.Json.Serialization;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents an event indicating that a voice session has started.
    /// </summary>
    public class VoiceStartedEventArgs
    {
        /// <summary>
        /// The ID of the voice session.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The unique identifier of the chat clan.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The ID of the voice channel.
        /// </summary>
        [JsonPropertyName("voice_channel_id")]
        public string VoiceChannelId { get; set; }
    }
}
