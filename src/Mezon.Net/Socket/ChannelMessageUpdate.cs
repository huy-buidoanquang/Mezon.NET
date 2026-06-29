using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for updating a message in a realtime chat channel.
    /// </summary>
    public class ChannelMessageUpdate : SocketSendBase
    {
        [JsonPropertyName("channel_message_update")]
        public ChannelMessageUpdateDetails ChannelMessageUpdateDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details of the message to be updated.
    /// </summary>
    public class ChannelMessageUpdateDetails
    {
        /// <summary>
        /// The server-assigned channel ID.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// A unique ID for the chat message to be updated.
        /// </summary>
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        /// <summary>
        /// The content payload, which can be any serializable object.
        /// </summary>
        [JsonPropertyName("content")]
        public object Content { get; set; }

        /// <summary>
        /// A list of mentions in the message.
        /// </summary>
        [JsonPropertyName("mentions")]
        public List<MessageMention>? Mentions { get; set; }

        /// <summary>
        /// A list of attachments in the message.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<MessageAttachment>? Attachments { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
