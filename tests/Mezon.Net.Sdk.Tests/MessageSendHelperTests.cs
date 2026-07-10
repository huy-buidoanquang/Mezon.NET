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
        public void ToChannelMessageSend_reply_includes_reference()
        {
            var reply = ToChannelMessageSend(new Mezon.Net.Models.ReplyMessageParams(1, 2, "reply", 1, true, 99, 5, "user"));
            Assert.Single(reply.References);
            Assert.Equal(99, reply.References[0].MessageRefId);
            Assert.Equal(5, reply.References[0].MessageSenderId);
        }

        private static ChannelMessageSend ToChannelMessageSend(SendChannelMessageParams message) =>
            InvokeToChannelMessageSend(message);

        private static ChannelMessageSend ToChannelMessageSend(Mezon.Net.Models.ReplyMessageParams message) =>
            InvokeToChannelMessageSend(message);

        private static ChannelMessageSend InvokeToChannelMessageSend<TMessage>(TMessage message)
        {
            var helperType = typeof(MezonClient).Assembly.GetType("Mezon.Net.Client.Messaging.MessageSendHelper")
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
    }
}
