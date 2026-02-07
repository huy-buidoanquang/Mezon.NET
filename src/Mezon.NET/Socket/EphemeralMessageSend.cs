using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for sending an ephemeral message to a user.
    /// </summary>
    public class EphemeralMessageSend : SocketSendBase
    {
        [JsonPropertyName("ephemeral_message_send")]
        public EphemeralMessageSendDetails EphemeralMessageSendDetails { get; set; }
    }

    /// <summary>
    /// Contains the top-level details for an ephemeral message, including the recipient and the message payload.
    /// </summary>
    public class EphemeralMessageSendDetails
    {
        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("message")]
        public EphemeralMessagePayload Message { get; set; }
    }

    /// <summary>
    /// Contains the detailed content of the ephemeral message to be sent.
    /// </summary>
    public class EphemeralMessagePayload
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

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }
    }
}
