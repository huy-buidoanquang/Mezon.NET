using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    // <summary>
    /// Represents the payload for sending a message to a realtime chat channel.
    /// </summary>
    public class ChannelMessageSend : SocketSendBase
    {
        [JsonPropertyName("channel_message_send")]
        public ChannelMessageSendDetails ChannelMessageSendDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details of the message to be sent.
    /// </summary>
    public class ChannelMessageSendDetails
    {
        /// <summary>
        /// The ID of the clan the channel belongs to.
        /// </summary>
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
        /// The content payload, which can be any serializable object.
        /// </summary>
        [JsonPropertyName("content")]
        public object Content { get; set; }

        [JsonPropertyName("mentions")]
        public List<MessageMention>? Mentions { get; set; }

        [JsonPropertyName("attachments")]
        public List<MessageAttachment>? Attachments { get; set; }

        [JsonPropertyName("anonymous_message")]
        public bool? AnonymousMessage { get; set; }

        [JsonPropertyName("mention_everyone")]
        public bool? MentionEveryone { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
