using System.Reflection;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Tests;

public sealed class EphemeralMessageWireTests
{
    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    public void ToEphemeralEnvelope_preserves_operation_message_and_recipient(int code)
    {
        var message = new SendEphemeralMessageParams(
            new[] { 7001L },
            new SendChannelMessageParams(
                clanId: 11,
                channelId: 22,
                content: code == 15 ? "{\"t\":\"deleteEphemeral\"}" : "{\"t\":\"updated\"}",
                isPublic: false,
                mode: 3,
                code: code,
                id: 9001));

        var envelope = MessageSendHelper.ToEphemeralEnvelope(message);
        var wire = envelope.EphemeralMessageSend;

        Assert.Equal(new[] { 7001L }, wire.ReceiverIds);
        Assert.Equal(11L, wire.Message.ClanId);
        Assert.Equal(22L, wire.Message.ChannelId);
        Assert.Equal(code, wire.Message.Code);
        Assert.Equal(9001L, wire.Message.Id);
        Assert.Equal(message.Message.Content, wire.Message.Content);
    }

    [Fact]
    public void ToEphemeralEnvelope_does_not_expand_recipient_scope()
    {
        var message = new SendEphemeralMessageParams(
            new[] { 101L },
            new SendChannelMessageParams(1, 2, "{\"t\":\"private\"}", code: 14, id: 55));

        var envelope = MessageSendHelper.ToEphemeralEnvelope(message);

        Assert.Single(envelope.EphemeralMessageSend.ReceiverIds);
        Assert.Equal(101L, envelope.EphemeralMessageSend.ReceiverIds[0]);
    }
}

public sealed class QuickMenuEventDispatchTests
{
    [Fact]
    public async Task QuickMenu_event_exposes_menu_source_and_author_metadata()
    {
        var client = new MezonClient();
        QuickMenuReceivedEventData? received = null;
        var legacyReceived = false;
        client.QuickMenuReceivedDataEvent += payload =>
        {
            received = payload;
            return Task.CompletedTask;
        };
        client.QuickMenuReceivedEvent += () =>
        {
            legacyReceived = true;
            return Task.CompletedTask;
        };

        await DispatchAsync(client, new Envelope
        {
            QuickMenuEvent = new QuickMenuDataEvent
            {
                MenuName = "ai-summary",
                SenderId = 10,
                MessageSenderId = 20,
                Message = new ChannelMessageSend
                {
                    ClanId = 30,
                    ChannelId = 40,
                    Id = 50,
                    Content = "{\"t\":\"summarize\"}",
                    Mode = 1,
                    IsPublic = true,
                },
            },
        });

        await WaitUntilAsync(() => received.HasValue && legacyReceived);

        var data = received!.Value;
        Assert.Equal("ai-summary", data.MenuName);
        Assert.Equal(10L, data.SenderId);
        Assert.Equal(20L, data.MessageSenderId);
        Assert.Equal(30L, data.ClanId);
        Assert.Equal(40L, data.ChannelId);
        Assert.Equal(50L, data.MessageId);
        Assert.Equal("{\"t\":\"summarize\"}", data.Message.Content);
    }

    [Fact]
    public async Task QuickMenu_event_with_missing_message_is_safe()
    {
        var client = new MezonClient();
        QuickMenuReceivedEventData? received = null;
        client.QuickMenuReceivedDataEvent += payload =>
        {
            received = payload;
            return Task.CompletedTask;
        };

        await DispatchAsync(client, new Envelope
        {
            QuickMenuEvent = new QuickMenuDataEvent { MenuName = "ai-summary" },
        });

        await Task.Delay(100);

        Assert.False(received.HasValue);
    }

    [Fact]
    public async Task QuickMenu_event_continues_after_a_subscriber_failure()
    {
        var client = new MezonClient();
        var secondSubscriberCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.QuickMenuReceivedDataEvent += _ => throw new InvalidOperationException("test failure");
        client.QuickMenuReceivedDataEvent += _ =>
        {
            secondSubscriberCalled.TrySetResult();
            return Task.CompletedTask;
        };

        await DispatchAsync(client, new Envelope
        {
            QuickMenuEvent = new QuickMenuDataEvent
            {
                MenuName = "ai-summary",
                Message = new ChannelMessageSend { Id = 1, ClanId = 2, ChannelId = 3 },
            },
        });

        var completed = await Task.WhenAny(secondSubscriberCalled.Task, Task.Delay(2000));
        Assert.Same(secondSubscriberCalled.Task, completed);
    }

    private static async Task DispatchAsync(MezonClient client, Envelope envelope)
    {
        var method = typeof(MezonClient).GetMethod("SocketMessageHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(client, new object?[] { MezonMessageType.Realtime, envelope.Cid, 0, (ReadOnlyMemory<byte>?)null, envelope })!;
        await task;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Timed out waiting for quick menu event dispatch.");
    }
}
