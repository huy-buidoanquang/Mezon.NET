using System.Reflection;
using System.Runtime.CompilerServices;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Queue;

namespace Mezon.Net.Client.Tests;

public sealed class SocketRateLimiterTests
{
    [Fact]
    public async Task Under_limit_completes_without_waiting()
    {
        var limiter = CreateLimiter(maxCount: 3, windowSeconds: 60);
        var started = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            await limiter.WaitAsync();
        }

        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds < 200);
    }

    [Fact]
    public async Task Over_limit_waits_until_window_allows_next_send()
    {
        var limiter = CreateLimiter(maxCount: 1, windowSeconds: 1);
        await limiter.WaitAsync();
        var started = DateTimeOffset.UtcNow;
        await limiter.WaitAsync();
        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds >= 900);
    }

    [Fact]
    public void Reset_clears_rate_limit_state()
    {
        var limiter = CreateLimiter(maxCount: 1, windowSeconds: 60);
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
        limiter.Reset();
        Assert.True(limiter.TryAcquire());
    }

    private static SocketRateLimiter CreateLimiter(int maxCount, int windowSeconds)
    {
        var type = typeof(SocketRateLimiter);
        return (SocketRateLimiter)Activator.CreateInstance(type, SocketBucketType.Unbucketed, maxCount, windowSeconds)!;
    }
}

public sealed class EventDispatchTests
{
    [Fact]
    public async Task ProcessMessageAsync_dispatches_new_realtime_oneof_events()
    {
        var client = new MezonClient();
        ApiRequestEvent? apiRequest = null;
        ListChannelUsersBannedEvent? banned = null;
        global::Mezon.Net.Internal.Api.Session? session = null;
        ChannelArchiveEvent? archive = null;
        TopicInMessageEvent? topic = null;

        client.ApiRequestReceivedEvent += payload => { apiRequest = payload; return Task.CompletedTask; };
        client.ListChannelUsersBannedEvent += payload => { banned = payload; return Task.CompletedTask; };
        client.RefreshSessionEvent += payload => { session = payload; return Task.CompletedTask; };
        client.ChannelArchiveEvent += payload => { archive = payload; return Task.CompletedTask; };
        client.TopicInMessageEvent += payload => { topic = payload; return Task.CompletedTask; };

        var envelopes = new[]
        {
            new Envelope { ApiRequestEvent = new ApiRequestEvent { ApiName = "Healthcheck", ApiIndex = 201 } },
            new Envelope { ListChannelUsersBannedEvent = new ListChannelUsersBannedEvent { BannedUserIds = { 42 } } },
            new Envelope { RefreshSessionEvent = new global::Mezon.Net.Internal.Api.Session { Token = "t" } },
            new Envelope { ChannelArchiveEvent = new ChannelArchiveEvent { ChannelId = 7, ClanId = 3 } },
            new Envelope { TopicInMessageEvent = new TopicInMessageEvent { MessageId = 99, TpId = "topic" } },
        };

        var process = typeof(MezonClient).GetMethod("ProcessMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var envelope in envelopes)
        {
            var task = (Task)process.Invoke(client, new object?[] { MezonMessageType.Abridged, envelope.Cid, 0, (ReadOnlyMemory<byte>?)null, envelope })!;
            await task;
        }

        Assert.Equal("Healthcheck", apiRequest?.ApiName);
        Assert.Contains(42L, banned?.BannedUserIds);
        Assert.Equal("t", session?.Token);
        Assert.Equal(7, archive?.ChannelId);
        Assert.Equal(99, topic?.MessageId);
    }
}

public sealed class AllocationSmokeTests
{
    [Fact]
    public void SendChannelMessageParams_struct_has_expected_size()
    {
        var size = Unsafe.SizeOf<Mezon.Net.Api.SendChannelMessageParams>();
        Assert.True(size < 64);
    }
}
