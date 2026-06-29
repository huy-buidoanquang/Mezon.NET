using System.Text.Json.Serialization;

namespace Mezon.NET.Abstractions.Events
{
    /// <summary>
    /// Represents a user typing event in a channel.
    /// </summary>
    public class MessageTypingEventArgs : MezonEventArgs
    {
        /// <summary>
        /// The channel this event belongs to.
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
        /// The ID of the user who is typing.
        /// </summary>
        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }

        /// <summary>
        /// The username of the sender.
        /// </summary>
        [JsonPropertyName("sender_username")]
        public string SenderUsername { get; set; }

        /// <summary>
        /// The display name of the sender.
        /// </summary>
        [JsonPropertyName("sender_display_name")]
        public string SenderDisplayName { get; set; }
    }
}
