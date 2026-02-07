using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for removing a message from a realtime chat channel.
    /// </summary>
    public class ChannelMessageRemove : SocketSendBase
    {
        [JsonPropertyName("channel_message_remove")]
        public ChannelMessageRemoveDetails ChannelMessageRemoveDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details of the message to be removed.
    /// </summary>
    public class ChannelMessageRemoveDetails
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The server-assigned channel ID.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// The channel label.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// A unique ID for the chat message to be removed.
        /// </summary>
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
