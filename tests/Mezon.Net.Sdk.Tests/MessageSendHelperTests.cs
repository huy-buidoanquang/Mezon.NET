using System;
using System.Reflection;
using Mezon.Net.Client;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class MessageSendHelperTests
    {
        [Fact]
        public void ToChannelMessageSend_maps_send_params()
        {
            var send = ToChannelMessageSend(new SendChannelMessageParams(1, 2, "hello", isPublic: true, mode: 4, code: 1));
            Assert.Equal(1, send.ClanId);
            Assert.Equal(2, send.ChannelId);
            Assert.Equal("hello", send.Content);
            Assert.True(send.IsPublic);
            Assert.Equal(4, send.Mode);
            Assert.Equal(1, send.Code);
        }

        [Fact]
        public void ToChannelMessageSend_maps_mentions_attachments_references()
        {
            var send = ToChannelMessageSend(new SendChannelMessageParams(
                1,
                2,
                "hello",
                mentions: new[] { new MessageMentionParams(userId: 42, username: "alice") },
                attachments: new[] { new MessageAttachmentParams(filename: "a.png", url: "https://cdn/a.png") },
                references: new[] { new MessageRefParams(messageRefId: 99, messageSenderId: 5) },
                avatar: "avatar.png",
                id: 123));

            Assert.Single(send.Mentions);
            Assert.Equal(42, send.Mentions[0].UserId);
            Assert.Equal("alice", send.Mentions[0].Username);
            Assert.Single(send.Attachments);
            Assert.Equal("a.png", send.Attachments[0].Filename);
            Assert.Equal("https://cdn/a.png", send.Attachments[0].Url);
            Assert.Single(send.References);
            Assert.Equal(99, send.References[0].MessageRefId);
            Assert.Equal(5, send.References[0].MessageSenderId);
            Assert.Equal("avatar.png", send.Avatar);
            Assert.Equal(123, send.Id);
        }

        [Fact]
        public void ToChannelMessageSend_reply_includes_reference()
        {
            var reply = ToChannelMessageSend(new ReplyMessageParams(1, 2, "reply", 1, true, 99, 5, "user"));
            Assert.Single(reply.References);
            Assert.Equal(99, reply.References[0].MessageRefId);
            Assert.Equal(5, reply.References[0].MessageSenderId);
        }

        [Fact]
        public void ToChannelMessageSend_reply_includes_mentions_and_attachments()
        {
            var reply = ToChannelMessageSend(new ReplyMessageParams(
                1,
                2,
                "reply",
                1,
                true,
                99,
                5,
                "user",
                mentions: new[] { new MessageMentionParams(userId: 42) },
                attachments: new[] { new MessageAttachmentParams(filename: "c.png", url: "https://cdn/c.png") }));

            Assert.Single(reply.References);
            Assert.Single(reply.Mentions);
            Assert.Equal(42, reply.Mentions[0].UserId);
            Assert.Single(reply.Attachments);
            Assert.Equal("c.png", reply.Attachments[0].Filename);
        }

        [Fact]
        public void ToChannelMessageUpdate_maps_mentions_and_attachments()
        {
            var update = InvokeToChannelMessageUpdate(new UpdateMessageParams(
                1,
                2,
                3,
                "updated",
                mode: 4,
                isPublic: true,
                mentions: new[] { new MessageMentionParams(userId: 7) },
                attachments: new[] { new MessageAttachmentParams(filename: "b.png", url: "https://cdn/b.png") }));

            Assert.Equal("updated", update.Content);
            Assert.Single(update.Mentions);
            Assert.Equal(7, update.Mentions[0].UserId);
            Assert.Single(update.Attachments);
            Assert.Equal("b.png", update.Attachments[0].Filename);
        }

        private static ChannelMessageSend ToChannelMessageSend(SendChannelMessageParams message) =>
            InvokeToChannelMessageSend(message);

        private static ChannelMessageSend ToChannelMessageSend(ReplyMessageParams message) =>
            InvokeToChannelMessageSend(message);

        private static ChannelMessageSend InvokeToChannelMessageSend<TMessage>(TMessage message)
        {
            var helperType = typeof(global::Mezon.Net.Client.MezonClient).Assembly.GetType("Mezon.Net.Client.Messaging.MessageSendHelper")
                ?? throw new InvalidOperationException("MessageSendHelper type was not found.");
            var parameterType = typeof(TMessage).MakeByRefType();

            foreach (var method in helperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var parameters = method.GetParameters();
                if (method.Name == "ToChannelMessageSend" && parameters.Length == 1 && parameters[0].ParameterType == parameterType)
                {
                    return (ChannelMessageSend)method.Invoke(null, new object?[] { message })!;
                }
            }

            throw new MissingMethodException(helperType.FullName, "ToChannelMessageSend");
        }

        private static ChannelMessageUpdate InvokeToChannelMessageUpdate(UpdateMessageParams message)
        {
            var helperType = typeof(global::Mezon.Net.Client.MezonClient).Assembly.GetType("Mezon.Net.Client.Messaging.MessageSendHelper")
                ?? throw new InvalidOperationException("MessageSendHelper type was not found.");
            var method = helperType.GetMethod("ToChannelMessageUpdate", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(helperType.FullName, "ToChannelMessageUpdate");
            return (ChannelMessageUpdate)method.Invoke(null, new object?[] { message })!;
        }
    }
}
