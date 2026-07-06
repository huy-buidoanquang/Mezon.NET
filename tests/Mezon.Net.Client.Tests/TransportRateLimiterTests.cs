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
    }

    [Fact]
    public async Task Over_limit_waits_until_window_allows_next_send()
    {
        var limiter = new SlidingWindowRateLimiter(maxCount: 1, windowSeconds: 1);
        await limiter.WaitAsync();
        var started = DateTimeOffset.UtcNow;
        await limiter.WaitAsync();
        Assert.True((DateTimeOffset.UtcNow - started).TotalMilliseconds >= 900);
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
}
