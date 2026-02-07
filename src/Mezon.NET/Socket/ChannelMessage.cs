using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a message sent on a channel.
    /// </summary>
    public class ChannelMessage
    {
        /// <summary>
        /// The unique ID of this message.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the chat room, or an empty string if this message was not sent through a chat room.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The clan this message belongs to.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The code representing a message type or category.
        /// </summary>
        [JsonPropertyName("code")]
        public int Code { get; set; }

        /// <summary>
        /// The content payload.
        /// </summary>
        [JsonPropertyName("content")]
        public ChannelMessageContent Content { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the message was created.
        /// </summary>
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("reactions")]
        public List<MessageReaction>? Reactions { get; set; }

        [JsonPropertyName("mentions")]
        public List<MessageMention>? Mentions { get; set; }

        [JsonPropertyName("attachments")]
        public List<MessageAttachment>? Attachments { get; set; }

        [JsonPropertyName("references")]
        public List<MessageRef>? References { get; set; }

        [JsonPropertyName("referenced_message")]
        public ChannelMessage? ReferencedMessage { get; set; }

        /// <summary>
        /// True if the message was persisted to the channel's history, false otherwise.
        /// </summary>
        [JsonPropertyName("persistent")]
        public bool? Persistent { get; set; }

        /// <summary>
        /// Message sender, usually a user ID.
        /// </summary>
        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the message was last updated.
        /// </summary>
        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        /// <summary>
        /// The username of the message sender, if any.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; }

        /// <summary>
        /// The clan nickname of the sender.
        /// </summary>
        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        /// <summary>
        /// The clan avatar of the sender.
        /// </summary>
        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonPropertyName("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("hide_editted")]
        public bool? HideEditted { get; set; }

        [JsonPropertyName("is_public")]
        public bool? IsPublic { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }
    }
}
