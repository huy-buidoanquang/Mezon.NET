using System.Collections.Generic;
using Mezon.Net.Core.Entities.Channels;
using Mezon.Protobuf.Api;

namespace Mezon.Net.WebSocket
{
    public class SocketChannelMessage : SocketEntity<ulong>, IChannelMessage
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public SocketChannelMessage(MezonClient socket, ulong id) : base(socket, id)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }

        public string Avatar { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the chat room, or an empty string if this message was not sent through a chat room.
        /// </summary>
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The clan this message belongs to.
        /// </summary>
        public string ClanId { get; set; }

        /// <summary>
        /// The code representing a message type or category.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// The content payload.
        /// </summary>
        public string Content { get; set; }

        public List<MessageReaction>? Reactions { get; set; }

        public List<MessageMention>? Mentions { get; set; }

        public List<MessageAttachment>? Attachments { get; set; }

        public List<MessageRef>? References { get; set; }

        public ChannelMessage? ReferencedMessage { get; set; }

        /// <summary>
        /// True if the message was persisted to the channel's history, false otherwise.
        /// </summary>
        public bool? Persistent { get; set; }

        /// <summary>
        /// Message sender, usually a user ID.
        /// </summary>
        public string SenderId { get; set; }

        /// <summary>
        /// The ISO string or UNIX time when the message was last updated.
        /// </summary>
        public string UpdateTime { get; set; }

        public string ClanLogo { get; set; }

        public string CategoryName { get; set; }

        /// <summary>
        /// The username of the message sender, if any.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The clan nickname of the sender.
        /// </summary>
        public string ClanNick { get; set; }

        /// <summary>
        /// The clan avatar of the sender.
        /// </summary>
        public string ClanAvatar { get; set; }

        public string DisplayName { get; set; }

        public long? CreateTimeSeconds { get; set; }

        public long? UpdateTimeSeconds { get; set; }

        public int? Mode { get; set; }

        public string MessageId { get; set; }

        public bool? HideEditted { get; set; }

        public bool? IsPublic { get; set; }

        public string TopicId { get; set; }
    }
}
