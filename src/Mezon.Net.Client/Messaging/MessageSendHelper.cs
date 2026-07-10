using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Core.Constants;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Messaging
{
    public static class MessageSendHelper
    {
        public static ChannelMessageSend ToChannelMessageSend(in SendChannelMessageParams message)
        {
            var body = new ChannelMessageSend
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                Content = message.Content,
                IsPublic = message.IsPublic,
                Mode = message.Mode,
                Code = message.Code,
                MentionEveryone = message.MentionEveryone,
                AnonymousMessage = message.AnonymousMessage,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

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

        public static Envelope ToEphemeralEnvelope(in SendChannelMessageParams message, long receiverId)
        {
            var envelope = new Envelope
            {
                EphemeralMessageSend = new EphemeralMessageSend
                {
                    Message = ToChannelMessageSend(message),
                },
            };
            envelope.EphemeralMessageSend.ReceiverIds.Add(receiverId);
            return envelope;
        }

        public static Task<ChannelMessageAck> SendAsync(IMezonApiClient api, in SendChannelMessageParams message, RequestOptions? options = null)
            => api.SendChannelMessageAsync(ToChannelMessageSend(message), options);

        public static Task<ChannelMessageAck> SendReplyAsync(IMezonApiClient api, in ReplyMessageParams message, RequestOptions? options = null)
            => api.SendChannelMessageAsync(ToChannelMessageSend(message), options);

        public static Task UpdateAsync(IMezonApiClient api, in UpdateMessageParams message, RequestOptions? options = null)
            => api.UpdateChannelMessageAsync(ToChannelMessageUpdate(message), options);

        public static Task DeleteAsync(IMezonApiClient api, in DeleteMessageParams message, RequestOptions? options = null)
            => api.DeleteChannelMessageAsync(ToChannelMessageRemove(message), options);

        public static Task ReactAsync(IMezonApiClient api, in ReactMessageParams message, RequestOptions? options = null)
            => api.ReactChannelMessageAsync(ToMessageReaction(message), options);
    }
}
