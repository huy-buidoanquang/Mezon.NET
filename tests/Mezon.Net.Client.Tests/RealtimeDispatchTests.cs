using System.Collections.Concurrent;
using System.Reflection;
using Mezon.Net.Client.Dispatch;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Logging;
using Mezon.Net.Models;
using ChannelMessage = Mezon.Net.Internal.Api.ChannelMessage;

namespace Mezon.Net.Client.Tests;

public sealed class RealtimeDispatchTests
{
    [Fact]
    public async Task Ordered_mode_preserves_per_channel_order()
    {
        var client = CreateClient();
        var received = new ConcurrentQueue<long>();
        client.ChannelMessageReceivedEvent += async message =>
        {
            await Task.Delay(Random.Shared.Next(0, 3));
            received.Enqueue(((ChannelMessageResponse)message).MessageId);
        };

        for (var id = 1; id <= 300; id++)
        {
            await DispatchAsync(client, Message(channelId: 7, messageId: id));
        }

        await WaitUntilAsync(() => received.Count == 300, timeoutMs: 10_000);
        Assert.Equal(Enumerable.Range(1, 300).Select(i => (long)i), received);
    }

    [Fact]
    public async Task Ordered_mode_runs_different_lanes_concurrently()
    {
        var client = CreateClient(laneCount: 16);
        var (channelA, channelB) = ChannelsOnDifferentLanes(16);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ChannelMessageReceivedEvent += async message =>
        {
            var channelId = ((ChannelMessageResponse)message).ChannelId;
            if (channelId == channelA)
            {
                await releaseA.Task;
            }
            else if (channelId == channelB)
            {
                receivedB.TrySetResult();
            }
        };

        await DispatchAsync(client, Message(channelA, 1));
        await DispatchAsync(client, Message(channelB, 2));

        await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(2));
        releaseA.TrySetResult();
    }

    [Fact]
    public async Task Slow_handler_stops_holding_its_lane_after_the_handler_timeout()
    {
        var client = CreateClient(handlerTimeoutMs: 200);
        var secondReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource();
        client.ChannelMessageReceivedEvent += async message =>
        {
            if (((ChannelMessageResponse)message).MessageId == 1)
            {
                await never.Task;
            }
            else
            {
                secondReceived.TrySetResult();
            }
        };

        await DispatchAsync(client, Message(7, 1));
        await DispatchAsync(client, Message(7, 2));

        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Full_lane_drops_new_events_without_blocking_the_receive_path()
    {
        var client = CreateClient(laneCount: 1, laneCapacity: 2, handlerTimeoutMs: 60_000);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ChannelMessageReceivedEvent += async _ =>
        {
            entered.TrySetResult();
            await release.Task;
        };

        await DispatchAsync(client, Message(7, 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var started = Environment.TickCount64;
        for (var id = 2; id <= 50; id++)
        {
            await DispatchAsync(client, Message(7, id));
        }

        Assert.True(Environment.TickCount64 - started < 1_000, "Dispatching must not wait for the blocked lane.");
        Assert.Equal(47, client.DroppedRealtimeEventCount);
        release.TrySetResult();
    }

    [Fact]
    public async Task Concurrent_mode_does_not_serialize_a_channel()
    {
        var client = CreateClient(mode: EventDispatchMode.Concurrent);
        var secondReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ChannelMessageReceivedEvent += async message =>
        {
            if (((ChannelMessageResponse)message).MessageId == 1)
            {
                await release.Task;
            }
            else
            {
                secondReceived.TrySetResult();
            }
        };

        await DispatchAsync(client, Message(7, 1));
        await DispatchAsync(client, Message(7, 2));

        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult();
    }

    [Fact]
    public async Task Handler_disposing_the_client_does_not_deadlock()
    {
        var client = CreateClient();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ChannelMessageReceivedEvent += async _ =>
        {
            await client.DisposeAsync();
            disposed.TrySetResult();
        };

        await DispatchAsync(client, Message(7, 1));

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Lane_key_uses_channel_then_clan()
    {
        Assert.Equal(5, RealtimeLaneKey.Compute(Message(5, 1)));
        Assert.Equal(7, RealtimeLaneKey.Compute(new Envelope { VoiceJoinedEvent = new VoiceJoinedEvent { VoiceChannelId = 7, ClanId = 3 } }));
        Assert.Equal(9, RealtimeLaneKey.Compute(new Envelope
        {
            UserChannelAddedEvent = new UserChannelAdded { ChannelDesc = new Mezon.Net.Internal.Api.ChannelDescription { ChannelId = 9 }, ClanId = 4 },
        }));
        Assert.Equal(11, RealtimeLaneKey.Compute(new Envelope { ClanUpdatedEvent = new ClanUpdatedEvent { ClanId = 11 } }));
        Assert.Equal(0, RealtimeLaneKey.Compute(new Envelope { Ping = new Ping() }));
        Assert.Equal(0, RealtimeLaneKey.Compute(new Envelope()));
    }

    [Fact]
    public async Task AsyncEvent_reports_several_subscriber_failures_together()
    {
        var asyncEvent = new AsyncEvent<Func<Task>>();
        asyncEvent.Add(() => throw new InvalidOperationException("first"));
        asyncEvent.Add(() => throw new ArgumentException("second"));

        var error = await Assert.ThrowsAsync<AggregateException>(() => asyncEvent.InvokeAsync());

        Assert.Collection(
            error.InnerExceptions,
            first => Assert.IsType<InvalidOperationException>(first),
            second => Assert.IsType<ArgumentException>(second));
    }

    [Fact]
    public async Task AsyncEvent_rethrows_a_single_failure_with_its_stack_trace()
    {
        var asyncEvent = new AsyncEvent<Func<Task>>();
        asyncEvent.Add(ThrowingSubscriber);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => asyncEvent.InvokeAsync());

        Assert.Contains(nameof(ThrowingSubscriber), error.StackTrace);
    }

    private static Task ThrowingSubscriber() => throw new InvalidOperationException("single");

    private static MezonClient CreateClient(
        EventDispatchMode mode = EventDispatchMode.Ordered,
        int laneCount = 16,
        int laneCapacity = 1024,
        int? handlerTimeoutMs = 3_000)
        => new(new MezonSocketClientOptions
        {
            EventDispatchMode = mode,
            EventDispatchLaneCount = laneCount,
            EventDispatchLaneCapacity = laneCapacity,
            SocketHandlerTimeoutInMilliseconds = handlerTimeoutMs,
            LogLevel = LogLevel.Error,
        });

    private static Envelope Message(long channelId, long messageId)
        => new() { ChannelMessage = new ChannelMessage { ClanId = 1, ChannelId = channelId, MessageId = messageId, Content = "{}" } };

    private static (long, long) ChannelsOnDifferentLanes(int laneCount)
    {
        var dispatcher = new RealtimeEventDispatcher(laneCount, 1, null, new LogManager(LogLevel.Error).CreateLogger("lanes"));
        try
        {
            for (long candidate = 2; ; candidate++)
            {
                if (dispatcher.LaneIndex(candidate) != dispatcher.LaneIndex(1))
                {
                    return (1, candidate);
                }
            }
        }
        finally
        {
            dispatcher.Cancel();
        }
    }

    private static Task DispatchAsync(MezonClient client, Envelope envelope)
    {
        var process = typeof(MezonClient).GetMethod("SocketMessageHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)process.Invoke(client, new object?[] { MezonMessageType.Realtime, envelope.Cid, 0, (ReadOnlyMemory<byte>?)null, envelope })!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }
}
