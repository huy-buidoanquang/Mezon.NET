using System.Collections.Generic;
using System.Text.Json.Serialization;
using Mezon.NET.Socket;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents a presence update for a particular realtime chat channel.
    /// </summary>
    public class ChannelPresenceEventArgs : MezonEventArgs
    {
        /// <summary>
        /// The unique identifier of the chat channel.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The channel name.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// Presences of the users who joined the channel.
        /// </summary>
        [JsonPropertyName("joins")]
        public List<Presence> Joins { get; set; }

        /// <summary>
        /// Presences of users who left the channel.
        /// </summary>
        [JsonPropertyName("leaves")]
        public List<Presence> Leaves { get; set; }
    }
}
