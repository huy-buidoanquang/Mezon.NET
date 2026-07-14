using System.Reflection;
using System.Runtime.CompilerServices;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Tests;

public sealed class EventDispatchTests
{
    [Fact]
    public async Task ProcessMessageAsync_dispatches_new_realtime_oneof_events()
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

        var process = typeof(MezonClient).GetMethod("ProcessMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var envelope in envelopes)
        {
            var task = (Task)process.Invoke(client, new object?[] { MezonMessageType.Realtime, envelope.Cid, 0, (ReadOnlyMemory<byte>?)null, envelope })!;
            await task;
        }

        Assert.Equal("Healthcheck", ((ApiRequestEventResponse)apiRequest!.Value).ApiName);
        Assert.True(banned.HasValue);
        Assert.Equal(42L, ((ListChannelUsersBannedEventResponse)banned.Value).BannedUserIds[0]);
        Assert.Equal(CreateJwt(), session?.AuthToken);
        Assert.Equal(7, ((ChannelArchiveEventResponse)archive!.Value).ChannelId);
        Assert.Equal(99, ((TopicInMessageEventResponse)topic!.Value).MessageId);
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
    public void SendChannelMessageParams_struct_has_expected_size()
    {
        var size = Unsafe.SizeOf<Mezon.Net.Models.SendChannelMessageParams>();
        Assert.True(size < 64);
    }
}
