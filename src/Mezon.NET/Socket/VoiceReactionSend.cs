using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for sending a voice reaction.
    /// </summary>
    public class VoiceReactionSend : SocketSendBase
    {
        /// <summary>
        /// A list of emojis for the reaction.
        /// </summary>
        [JsonPropertyName("emojis")]
        public List<string> Emojis { get; set; }

        /// <summary>
        /// The ID of the channel.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The ID of the sender.
        /// </summary>
        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        /// <summary>
        /// The type of media.
        /// </summary>
        [JsonPropertyName("media_type")]
        public int MediaType { get; set; }
    }
}
