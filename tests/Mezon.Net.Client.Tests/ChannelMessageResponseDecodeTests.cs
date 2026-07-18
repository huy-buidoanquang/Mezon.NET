using Google.Protobuf;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;
using Xunit;

namespace Mezon.Net.Client.Tests;

public sealed class ChannelMessageResponseDecodeTests
{
    [Fact]
    public void Decode_empty_nested_bytes_returns_empty_views_without_throwing()
    {
        var response = ChannelMessageResponse.Decode(new ChannelMessage
        {
            MessageId = 1,
            Content = "hello",
        });

        Assert.Equal(1, response.MessageId);
        Assert.Equal(0, response.Mentions.Count);
        Assert.Equal(0, response.Attachments.Count);
        Assert.Equal(0, response.References.Count);
        Assert.Equal(0, response.Reactions.Count);
        Assert.True(response.ReferencedMessage.IsEmpty);
    }

    [Fact]
    public void Decode_parses_mentions_attachments_references_reactions_from_bytes()
    {
        var mentions = new MessageMentionList
        {
            Mentions = { new MessageMention { UserId = 42, Username = "alice", S = 1, E = 6 } },
        };
        var attachments = new MessageAttachmentList
        {
            Attachments = { new MessageAttachment { Filename = "a.png", Url = "https://cdn/a.png" } },
        };
        var references = new MessageRefList
        {
            Refs = { new MessageRef { MessageRefId = 99, MessageSenderId = 5 } },
        };
        var reactions = new MessageReactionList
        {
            Reactions = { new MessageReaction { EmojiId = 7, Emoji = ":smile:", Count = 2 } },
        };

        var response = ChannelMessageResponse.Decode(new ChannelMessage
        {
            MessageId = 10,
            Mentions = mentions.ToByteString(),
            Attachments = attachments.ToByteString(),
            References = references.ToByteString(),
            Reactions = reactions.ToByteString(),
        });

        Assert.Equal(10, response.MessageId);
        Assert.Equal(1, response.Mentions.Count);
        Assert.Equal(42, response.Mentions[0].UserId);
        Assert.Equal("alice", response.Mentions[0].Username);
        Assert.Equal(1, response.Attachments.Count);
        Assert.Equal("a.png", response.Attachments[0].Filename);
        Assert.Equal(1, response.References.Count);
        Assert.Equal(99, response.References[0].MessageRefId);
        Assert.Equal(1, response.Reactions.Count);
        Assert.Equal(7, response.Reactions[0].EmojiId);
        Assert.Equal(2, response.Reactions[0].Count);
    }
}
