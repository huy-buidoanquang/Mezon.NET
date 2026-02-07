using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for joining a realtime chat channel.
    /// </summary>
    public class ChannelJoin : SocketSendBase
    {
        [JsonPropertyName("channel_join")]
        public ChannelJoinDetails ChannelJoinDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details of the channel to be joined.
    /// </summary>
    public class ChannelJoinDetails
    {
        /// <summary>
        /// The ID of the channel to join.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the channel to join.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The channel type: 1 = Channel, 2 = Direct Message, 3 = Group.
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        /// <summary>
        /// Whether channel messages are persisted in the database.
        /// </summary>
        [JsonPropertyName("persistence")]
        public bool Persistence { get; set; }

        /// <summary>
        /// Whether the user's channel presence is hidden when joining.
        /// </summary>
        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
