using System.Collections.Generic;
using Mezon.NET.Core.Entities.Channels;
using Mezon.Protobuf.Api;
using Newtonsoft.Json;

namespace Mezon.NET.WebSocket
{
    public class SocketChannelMessage : SocketEntity<ulong>, IChannelMessage
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public SocketChannelMessage(MezonClient socket, ulong id) : base(socket, id)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the chat room, or an empty string if this message was not sent through a chat room.
        /// </summary>
        [JsonProperty("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The clan this message belongs to.
        /// </summary>
        [JsonProperty("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The code representing a message type or category.
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// The content payload.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("reactions")]
        public List<MessageReaction>? Reactions { get; set; }

        [JsonProperty("mentions")]
        public List<MessageMention>? Mentions { get; set; }

        [JsonProperty("attachments")]
        public List<MessageAttachment>? Attachments { get; set; }

        [JsonProperty("references")]
        public List<MessageRef>? References { get; set; }

        [JsonProperty("referenced_message")]
        public ChannelMessage? ReferencedMessage { get; set; }

        /// <summary>
        /// True if the message was persisted to the channel's history, false otherwise.
        /// </summary>
        [JsonProperty("persistent")]
        public bool? Persistent { get; set; }

        /// <summary>
        /// Message sender, usually a user ID.
        /// </summary>
        [JsonProperty("sender_id")]
        public string SenderId { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the message was last updated.
        /// </summary>
        [JsonProperty("update_time")]
        public string UpdateTime { get; set; }

        [JsonProperty("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonProperty("category_name")]
        public string CategoryName { get; set; }

        /// <summary>
        /// The username of the message sender, if any.
        /// </summary>
        [JsonProperty("username")]
        public string Username { get; set; }

        /// <summary>
        /// The clan nickname of the sender.
        /// </summary>
        [JsonProperty("clan_nick")]
        public string ClanNick { get; set; }

        /// <summary>
        /// The clan avatar of the sender.
        /// </summary>
        [JsonProperty("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonProperty("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonProperty("mode")]
        public int? Mode { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("hide_editted")]
        public bool? HideEditted { get; set; }

        [JsonProperty("is_public")]
        public bool? IsPublic { get; set; }

        [JsonProperty("topic_id")]
        public string TopicId { get; set; }
    }
}
