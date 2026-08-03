using Mezon.Net.Core;
using Mezon.Net.Queue;

namespace Mezon.Net.Client.Tests;

public sealed class SlidingWindowRateLimiterTests
{
    [Fact]
    public async Task Under_limit_completes_without_waiting()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 3, windowSeconds: 60);
        var started = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            await limiter.WaitAsync();
        }

        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds < 200);
        Assert.Equal(3, limiter.CurrentCount);
    }

    [Fact]
    public async Task Over_limit_waits_until_window_allows_next_send()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 1, windowSeconds: 1);
        await limiter.WaitAsync();
        var started = DateTimeOffset.UtcNow;
        await limiter.WaitAsync();
        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds >= 900);
        Assert.Equal(1, limiter.CurrentCount);
    }

    [Fact]
    public async Task After_wait_slot_is_acquired()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 1, windowSeconds: 1);
        await limiter.WaitAsync();
        Assert.Equal(1, limiter.CurrentCount);
        Assert.False(limiter.TryAcquire());

        await limiter.WaitAsync();
        Assert.Equal(1, limiter.CurrentCount);
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public async Task Parallel_stampede_does_not_exceed_capacity()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 3, windowSeconds: 2);
        var tasks = new Task[12];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = limiter.WaitAsync().AsTask();
        }

        await Task.WhenAll(tasks);
        Assert.True(limiter.CurrentCount <= 3);
    }

    [Fact]
    public void Reset_clears_rate_limit_state()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 1, windowSeconds: 60);
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
        limiter.Reset();
        Assert.True(limiter.TryAcquire());
    }
}

public sealed class TransportRateLimiterTests
{
    [Fact]
    public async Task Connect_phase_applies_additional_limit()
    {
        var limiter = new TransportRateLimiter(maxRequestsPerSecond: 60, maxRequestsPerMinute: 200, maxConnectRequestsPerSecond: 1);
        limiter.BeginConnectPhase();
        await limiter.EnterAsync();
        var started = DateTimeOffset.UtcNow;
        await limiter.EnterAsync();
        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds >= 900);
        limiter.EndConnectPhase();
    }

    [Fact]
    public async Task Reset_clears_connect_phase()
    {
        var limiter = new TransportRateLimiter(maxRequestsPerSecond: 60, maxRequestsPerMinute: 200, maxConnectRequestsPerSecond: 1);
        limiter.BeginConnectPhase();
        await limiter.EnterAsync();
        limiter.Reset();
        var started = DateTimeOffset.UtcNow;
        await limiter.EnterAsync();
        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds < 200);
    }

    [Fact]
    public async Task Ratelimit_callback_is_invoked_when_delayed()
    {
        var limiter = new TransportRateLimiter(maxRequestsPerSecond: 1, maxRequestsPerMinute: 200, maxConnectRequestsPerSecond: 10);
        IRateLimitInfo? seen = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await limiter.EnterAsync();
        await limiter.EnterAsync(default, info =>
        {
            seen = info;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(seen);
        Assert.Equal(RateLimitBuckets.TransportPerSecond, seen!.Bucket);
        Assert.True(seen.IsGlobal);
        Assert.Equal(1, seen.Limit);
        Assert.Equal(0, seen.Remaining);
        Assert.True(seen.ResetAfter > TimeSpan.Zero);
        Assert.Null(seen.SendBypassMessageAsync);
    }

    [Fact]
    public async Task Ratelimit_callback_receives_bypass_send_delegate()
    {
        var limiter = new TransportRateLimiter(maxRequestsPerSecond: 1, maxRequestsPerMinute: 200, maxConnectRequestsPerSecond: 10);
        long? seenClan = null;
        long? seenChannel = null;
        string? seenText = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<long, long, string, Task> bypass = (clanId, channelId, text) =>
        {
            seenClan = clanId;
            seenChannel = channelId;
            seenText = text;
            return Task.CompletedTask;
        };

        await limiter.EnterAsync();
        await limiter.EnterAsync(
            default,
            async info =>
            {
                Assert.NotNull(info.SendBypassMessageAsync);
                await info.SendBypassMessageAsync!(9, 99, "slow down");
                tcs.TrySetResult(true);
            },
            bypass);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(9, seenClan);
        Assert.Equal(99, seenChannel);
        Assert.Equal("slow down", seenText);
    }

    [Fact]
    public void Configure_updates_limits_in_place()
    {
        var limiter = new TransportRateLimiter(maxRequestsPerSecond: 1, maxRequestsPerMinute: 10, maxConnectRequestsPerSecond: 1);
        limiter.Configure(60, 500, 2);
        Assert.True(limiter.EnterAsync().IsCompletedSuccessfully);
    }
}

public sealed class SocketCorrelationHubTests
{
    [Fact]
    public async Task Register_does_not_timeout_before_timeout_is_started()
    {
        var hub = new SocketCorrelationHub();
        var pending = hub.Register(cid: 42);

        await Task.Delay(150);
        Assert.False(pending.Task.IsCompleted);

        Assert.True(hub.TryComplete(42, 0, ReadOnlyMemory<byte>.Empty));
        var response = await pending.Task;
        Assert.Equal(0, response.Code);
    }

    [Fact]
    public async Task Timeout_starts_only_after_explicit_start()
    {
        var hub = new SocketCorrelationHub();
        var pending = hub.Register(cid: 77);

        await Task.Delay(150);
        pending.StartTimeout(100);

        await Assert.ThrowsAsync<TimeoutException>(async () => await pending.Task);
    }
}
