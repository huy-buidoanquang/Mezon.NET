using System.Reflection;
using System.Runtime.CompilerServices;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Tests;

public sealed class EventDispatchTests
{
    [Fact]
    public async Task SocketMessageHandlerAsync_dispatches_new_realtime_oneof_events()
    {
        var client = new MezonClient();
        ApiRequestEventEventData? apiRequest = null;
        ListChannelUsersBannedEventEventData? banned = null;
        Session? session = null;
        ChannelArchiveEventEventData? archive = null;
        TopicInMessageEventEventData? topic = null;

        client.ApiRequestReceivedEvent += payload => { apiRequest = payload; return Task.CompletedTask; };
        client.ChannelUsersBannedListedEvent += payload => { banned = payload; return Task.CompletedTask; };
        client.SessionRefreshedEvent += payload => { session = payload; return Task.CompletedTask; };
        client.ChannelArchivedEvent += payload => { archive = payload; return Task.CompletedTask; };
        client.TopicInMessageReceivedEvent += payload => { topic = payload; return Task.CompletedTask; };

        var envelopes = new[]
        {
            new Envelope { ApiRequestEvent = new ApiRequestEvent { ApiName = "Healthcheck", ApiIndex = 201 } },
            new Envelope { ListChannelUsersBannedEvent = new ListChannelUsersBannedEvent { BannedUserIds = { 42 } } },
            new Envelope { RefreshSessionEvent = new global::Mezon.Net.Internal.Api.Session { SessionId = "s", Token = CreateJwt(), RefreshToken = CreateJwt() } },
            new Envelope { ChannelArchiveEvent = new ChannelArchiveEvent { ChannelId = 7, ClanId = 3 } },
            new Envelope { TopicInMessageEvent = new TopicInMessageEvent { MessageId = 99, TpId = "topic" } },
        };

        await DispatchAllAsync(client, envelopes);
        await WaitUntilAsync(() => apiRequest.HasValue && banned.HasValue && session != null && archive.HasValue && topic.HasValue);

        Assert.Equal("Healthcheck", ((ApiRequestEventResponse)apiRequest!.Value).ApiName);
        Assert.Equal(42L, ((ListChannelUsersBannedEventResponse)banned!.Value).BannedUserIds[0]);
        Assert.Equal(CreateJwt(), session?.AuthToken);
        Assert.Equal(7, ((ChannelArchiveEventResponse)archive!.Value).ChannelId);
        Assert.Equal(99, ((TopicInMessageEventResponse)topic!.Value).MessageId);
    }

    [Fact]
    public async Task SocketMessageHandlerAsync_dispatches_interaction_wire_payloads()
    {
        var client = new MezonClient();
        MessageButtonClickedEventData? buttonClick = null;
        DropdownBoxSelectedEventData? dropdown = null;

        client.MessageButtonClickedEvent += payload =>
        {
            buttonClick = payload;
            return Task.CompletedTask;
        };
        client.DropdownBoxSelectedEvent += payload =>
        {
            dropdown = payload;
            return Task.CompletedTask;
        };

        var envelopes = new[]
        {
            new Envelope
            {
                MessageButtonClicked = new MessageButtonClicked
                {
                    MessageId = 1001,
                    ChannelId = 2002,
                    ButtonId = "btn-confirm",
                    SenderId = 3003,
                    UserId = 4004,
                    ExtraData = "{\"key\":\"value\"}",
                },
            },
            new Envelope
            {
                DropdownBoxSelected = new DropdownBoxSelected
                {
                    MessageId = 5005,
                    ChannelId = 6006,
                    SelectboxId = "select-priority",
                    SenderId = 7007,
                    UserId = 8008,
                    Values = { "high", "urgent" },
                },
            },
        };

        await DispatchAllAsync(client, envelopes);
        await WaitUntilAsync(() => buttonClick.HasValue && dropdown.HasValue);

        var button = (MessageButtonClickedResponse)buttonClick!.Value;
        Assert.Equal(1001L, button.MessageId);
        Assert.Equal(2002L, button.ChannelId);
        Assert.Equal("btn-confirm", button.ButtonId);
        Assert.Equal(3003L, button.SenderId);
        Assert.Equal(4004L, button.UserId);
        Assert.Equal("{\"key\":\"value\"}", button.ExtraData);

        var select = (DropdownBoxSelectedResponse)dropdown!.Value;
        Assert.Equal(5005L, select.MessageId);
        Assert.Equal(6006L, select.ChannelId);
        Assert.Equal("select-priority", select.SelectboxId);
        Assert.Equal(7007L, select.SenderId);
        Assert.Equal(8008L, select.UserId);
        Assert.Equal(2, select.Values.Count);
        Assert.Equal("high", select.Values[0]);
        Assert.Equal("urgent", select.Values[1]);
    }

    private static async Task DispatchAllAsync(MezonClient client, Envelope[] envelopes)
    {
        var process = typeof(MezonClient).GetMethod("SocketMessageHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var envelope in envelopes)
        {
            var task = (Task)process.Invoke(client, new object?[] { MezonMessageType.Realtime, envelope.Cid, 0, (ReadOnlyMemory<byte>?)null, envelope })!;
            await task;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Timed out waiting for event dispatch.");
    }

    private static string CreateJwt()
    {
        const string header = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0";
        const string payload = "eyJleHAiOjk5OTk5OTk5OTksInVpZCI6IjEiLCJ1c24iOiJ0ZXN0In0";
        return $"{header}.{payload}.";
    }
}

public sealed class AllocationSmokeTests
{
    [Fact]
    public void SendChannelMessageParams_struct_stays_compact_for_hot_path()
    {
        var size = Unsafe.SizeOf<Mezon.Net.Models.SendChannelMessageParams>();
        Assert.True(size <= 128);
    }
}
