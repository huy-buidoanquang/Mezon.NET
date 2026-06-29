using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions.Events;
using Mezon.NET.Api;
using Mezon.NET.Socket;

namespace Mezon.NET.Abstractions
{
    public interface ISocketC2S
    {
        /// <summary>
        /// Joins a clan chat.
        /// </summary>
        Task JoinClanChatAsync(ClanJoin clanJoin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Joins a chat channel on the server.
        /// </summary>
        Task<Channel> JoinChannelChatAsync(ChannelJoin channelJoin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Leaves a chat channel on the server.
        /// </summary>
        Task LeaveChannelChatAsync(string clanId, string channelId, int channelType, bool isPublic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a chat message from a chat channel on the server.
        /// </summary>
        Task<ChannelMessageAck> RemoveChatMessageAsync(string clanId, string channelId, int mode, bool isPublic, string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a chat message on a chat channel in the server.
        /// </summary>
        Task<ChannelMessageAck> UpdateChatMessageAsync(string clanId, string channelId, int mode, bool isPublic, string messageId, object content, IEnumerable<ApiMessageMention>? mentions = null, IEnumerable<ApiMessageAttachment>? attachments = null, bool? hideEdited = null, string? topicId = null, bool? isUpdateMsgTopic = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status for the current user online.
        /// </summary>
        Task UpdateStatusAsync(string? status = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a chat message to a chat channel on the server.
        /// </summary>
        Task<ChannelMessageAck> WriteChatMessageAsync(string clanId, string channelId, int mode, bool isPublic, object? content = null, IEnumerable<ApiMessageMention>? mentions = null, IEnumerable<ApiMessageAttachment>? attachments = null, IEnumerable<ApiMessageRef>? references = null, bool? anonymousMessage = null, bool? mentionEveryone = null, string? avatar = null, int? code = null, string? topicId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message typing indicator.
        /// </summary>
        //Task<MessageTypingEvent> WriteMessageTypingAsync(string clanId, string channelId, int mode, bool isPublic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message reaction.
        /// </summary>
        Task<ApiMessageReaction> WriteMessageReactionAsync(string id, string clanId, string channelId, int mode, bool isPublic, string messageId, string emojiId, string emoji, int count, string messageSenderId, bool actionDelete, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a token to another user.
        /// </summary>
        //Task<TokenSentEvent> SendTokenAsync(string receiverId, decimal amount, CancellationToken cancellationToken = default);
    }
}
