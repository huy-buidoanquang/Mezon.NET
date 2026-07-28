using System.Threading.Tasks;
using Mezon.Net.Client.Models.Internal;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Messaging
{
    internal static class MessageSendHelper
    {
        internal static void Fill(ChannelMessageSend body, in SendChannelMessageParams message)
        {
            body.ClanId = message.ClanId;
            body.ChannelId = message.ChannelId;
            body.Content = message.Content;
            body.IsPublic = message.IsPublic;
            body.Mode = message.Mode;
            body.Code = message.Code;
            body.MentionEveryone = message.MentionEveryone;
            body.AnonymousMessage = message.AnonymousMessage;
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            if (message.Avatar is not null)
            {
                body.Avatar = message.Avatar;
            }

            if (message.Id.HasValue)
            {
                body.Id = message.Id.Value;
            }

            if (message.Mentions is not null)
            {
                foreach (var item in message.Mentions)
                {
                    body.Mentions.Add(MessageMentionParamsMapper.ToProto(item));
                }
            }

            if (message.Attachments is not null)
            {
                foreach (var item in message.Attachments)
                {
                    body.Attachments.Add(MessageAttachmentParamsMapper.ToProto(item));
                }
            }

            if (message.References is not null)
            {
                foreach (var item in message.References)
                {
                    body.References.Add(MessageRefParamsMapper.ToProto(item));
                }
            }
        }

        public static ChannelMessageSend ToChannelMessageSend(in SendChannelMessageParams message)
        {
            var body = new ChannelMessageSend();
            Fill(body, in message);
            return body;
        }

        public static SendChannelMessageParams ToSendParams(in ReplyMessageParams message)
        {
            var mode = ChannelModeConverter.ToStreamMode(message.ChannelType);
            var reference = new MessageRefParams(
                messageRefId: message.ReplyToMessageId,
                messageSenderId: message.ReplyToSenderId,
                content: message.ReplyToContent,
                refType: 0,
                messageSenderUsername: message.ReplyToSenderUsername,
                messageSenderAvatar: message.ReplyToSenderAvatar);

            return new SendChannelMessageParams(
                message.ClanId,
                message.ChannelId,
                message.Content,
                message.TopicId,
                message.IsPublic,
                mode,
                message.Code,
                message.MentionEveryone,
                message.AnonymousMessage,
                mentions: message.Mentions,
                attachments: message.Attachments,
                references: new[] { reference });
        }

        public static ChannelMessageSend ToChannelMessageSend(in ReplyMessageParams message)
            => ToChannelMessageSend(ToSendParams(message));

        public static ChannelMessageUpdate ToChannelMessageUpdate(in UpdateMessageParams message)
            => ChannelMessageUpdateParamsMapper.ToProto(new ChannelMessageUpdateParams(
                message.ClanId,
                message.ChannelId,
                message.MessageId,
                message.Content,
                message.Mentions,
                message.Attachments,
                message.Mode,
                message.IsPublic,
                message.HideEdited,
                message.TopicId,
                message.IsUpdateMsgTopic,
                message.CreateTimeSeconds));

        public static ChannelMessageRemove ToChannelMessageRemove(in DeleteMessageParams message)
        {
            var body = new ChannelMessageRemove
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                MessageId = message.MessageId,
                Mode = message.Mode,
                IsPublic = message.IsPublic,
                HasAttachment = message.HasAttachment,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            return body;
        }

        public static MessageReaction ToMessageReaction(in ReactMessageParams message)
        {
            var body = new MessageReaction
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                MessageId = message.MessageId,
                EmojiId = message.EmojiId,
                Emoji = message.Emoji,
                Mode = message.Mode,
                IsPublic = message.IsPublic,
                SenderId = message.SenderId,
                Action = message.Action,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            return body;
        }

        public static Envelope ToEphemeralEnvelope(in SendEphemeralMessageParams message)
        {
            var body = ToChannelMessageSend(message.Message);
            if (message.Id.HasValue)
            {
                body.Id = message.Id.Value;
            }

            var envelope = new Envelope
            {
                EphemeralMessageSend = new EphemeralMessageSend
                {
                    Message = body,
                },
            };
            foreach (var receiverId in message.ReceiverIds)
            {
                envelope.EphemeralMessageSend.ReceiverIds.Add(receiverId);
            }

            return envelope;
        }

        public static Envelope ToQuickMenuEnvelope(in QuickMenuDataEventParams message)
        {
            var body = ToChannelMessageSend(message.Message);
            if (message.MessageId.HasValue)
            {
                body.Id = message.MessageId.Value;
            }

            var quickMenu = new QuickMenuDataEvent
            {
                MenuName = message.MenuName,
                Message = body,
            };
            if (message.MessageSenderId.HasValue)
            {
                quickMenu.MessageSenderId = message.MessageSenderId.Value;
            }

            return new Envelope { QuickMenuEvent = quickMenu };
        }

        internal static Task<ChannelMessageAckResponse> SendAsync(MezonClient client, in SendChannelMessageParams message, RequestOptions? options = null)
            => client.SendChatMessageRtAsync(message, options);

        internal static Task<ChannelMessageAckResponse> SendReplyAsync(MezonClient client, in ReplyMessageParams message, RequestOptions? options = null)
            => client.SendChatMessageRtAsync(ToSendParams(message), options);

        /// <summary>
        /// Updates a message via socket API <c>UpdateChannelMessage</c> (same path as mezon-sdk bots).
        /// The realtime envelope <c>channel_message_update</c> may ack without UI clients receiving <c>ChatUpdate</c>.
        /// </summary>
        internal static Task UpdateAsync(MezonClient client, in UpdateMessageParams message, RequestOptions? options = null)
            => client.UpdateChannelMessageAsync(new ChannelMessageUpdateParams(
                message.ClanId,
                message.ChannelId,
                message.MessageId,
                message.Content,
                message.Mentions,
                message.Attachments,
                message.Mode,
                message.IsPublic,
                message.HideEdited,
                message.TopicId,
                message.IsUpdateMsgTopic,
                message.CreateTimeSeconds), options);

        internal static Task DeleteAsync(MezonClient client, in DeleteMessageParams message, RequestOptions? options = null)
            => client.RemoveChatMessageRtAsync(message, options);

        internal static Task ReactAsync(MezonClient client, in ReactMessageParams message, RequestOptions? options = null)
            => client.SendMessageReactionRtAsync(message, options);
    }
}
