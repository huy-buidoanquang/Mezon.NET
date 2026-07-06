using Mezon.Net.Client.Messaging;
using Mezon.Net.Api;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class MessageSendHelperTests
    {
        [Fact]
        public void ToChannelMessageSend_maps_send_params()
        {
            var send = MessageSendHelper.ToChannelMessageSend(new SendChannelMessageParams(1, 2, "hello", isPublic: true, mode: 4, code: 1));
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
            var reply = MessageSendHelper.ToChannelMessageSend(new ReplyMessageParams(1, 2, "reply", 1, true, 99, 5, "user"));
            Assert.Single(reply.References);
            Assert.Equal(99, reply.References[0].MessageRefId);
            Assert.Equal(5, reply.References[0].MessageSenderId);
        }
    }
}
