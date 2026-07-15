using System.Threading.Tasks;
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
        }

        public static ChannelMessageSend ToChannelMessageSend(in SendChannelMessageParams message)
        {
            var body = new ChannelMessageSend();
            Fill(body, in message);
            return body;
        }

        public static ChannelMessageSend ToChannelMessageSend(in ReplyMessageParams message)
        {
            var mode = ChannelModeConverter.ToStreamMode(message.ChannelType);
            var body = new ChannelMessageSend
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                Content = message.Content,
                IsPublic = message.IsPublic,
                Mode = mode,
                MentionEveryone = message.MentionEveryone,
                AnonymousMessage = message.AnonymousMessage,
                Code = message.Code,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            var reference = new MessageRef
            {
                MessageRefId = message.ReplyToMessageId,
                MessageSenderId = message.ReplyToSenderId,
                RefType = 0,
            };
            if (!string.IsNullOrEmpty(message.ReplyToSenderUsername))
            {
                reference.MessageSenderUsername = message.ReplyToSenderUsername;
            }
            if (!string.IsNullOrEmpty(message.ReplyToSenderAvatar))
            {
                reference.MessageSenderAvatar = message.ReplyToSenderAvatar;
            }
            if (!string.IsNullOrEmpty(message.ReplyToContent))
            {
                reference.Content = message.ReplyToContent;
            }

            body.References.Add(reference);
            return body;
        }

        public static ChannelMessageUpdate ToChannelMessageUpdate(in UpdateMessageParams message)
        {
            var body = new ChannelMessageUpdate
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                MessageId = message.MessageId,
                Content = message.Content,
                Mode = message.Mode,
                IsPublic = message.IsPublic,
                HideEditted = message.HideEdited,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            return body;
        }

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
            => client.SendChatMessageRtAsync(new SendChannelMessageParams(
                message.ClanId,
                message.ChannelId,
                message.Content,
                message.TopicId,
                message.IsPublic,
                code: message.Code,
                mentionEveryone: message.MentionEveryone), options);

        internal static Task UpdateAsync(MezonClient client, in UpdateMessageParams message, RequestOptions? options = null)
            => client.UpdateChatMessageRtAsync(message, options);

        internal static Task DeleteAsync(MezonClient client, in DeleteMessageParams message, RequestOptions? options = null)
            => client.RemoveChatMessageRtAsync(message, options);

        internal static Task ReactAsync(MezonClient client, in ReactMessageParams message, RequestOptions? options = null)
            => client.SendMessageReactionRtAsync(message, options);
    }
}
