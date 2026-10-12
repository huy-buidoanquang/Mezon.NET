using System.Diagnostics;
using Mezon.Net.Sdk.Sfu.Peer;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

public class SessionRecoveryTests
{
    [Fact]
    public async Task A_peer_connection_closed_by_the_stack_reconnects()
    {
        await using var h = Harness.Create();
        await h.Connect();
        await h.WaitEvent("state:Connected");
        h.Sfu.Peer.Config.OnState(PeerState.Closed);
        await h.WaitEvent("reconnecting");
        await h.Wait("a second connection", () => h.Sfu.ConnCount == 2);
    }

    [Fact]
    public async Task A_fatal_answer_error_ends_the_session_at_once()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Admission = TimeSpan.FromSeconds(10) } });
        h.Sfu.NeverConnect = true;
        h.Sfu.On("join", (_, _) =>
        {
            if (h.Sfu.ConnCount > 1)
            {
                h.Sfu.NeverConnect = false;
            }

            return false;
        });
        h.Sfu.On("answer", (c, _) =>
        {
            if (h.Sfu.ConnCount > 1)
            {
                return false;
            }

            c.Send(new { type = "error", message = "duplicate_ice_ufrag" });
            return true;
        });
        var watch = Stopwatch.StartNew();
        await h.Connect();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"the join waited {watch.Elapsed.TotalSeconds:F1}s for the admission timeout");
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Dispose_during_the_connect_backoff_ends_connect_promptly()
    {
        var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(30) } });
        h.Sfu.Refuse = true;
        var connect = h.Connect();
        await Task.Delay(500);
        await h.Room.DisposeAsync();
        var finished = await Task.WhenAny(connect, Task.Delay(3000)) == connect;
        await Assert.ThrowsAnyAsync<Exception>(() => connect);
        await h.DisposeAsync();
        Assert.True(finished, "ConnectAsync kept sleeping through its backoff after the room was disposed");
    }

    [Fact]
    public async Task A_fixed_token_rejected_during_a_reconnect_ends_with_auth_failed()
    {
        await using var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(1) } });
        await h.Room.ConnectAsync(h.Sfu.Url, TokenProvider.Fixed(FakeSfu.Token("7", 3600)));
        await h.WaitEvent("state:Connected");
        var watch = Stopwatch.StartNew();
        h.Sfu.Current.Close(4005);
        await h.WaitEvent("disconnected:AuthFailed", 15);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"ended after {watch.Elapsed.TotalSeconds:F1}s");
        Assert.Equal(1, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task An_earlier_session_does_not_reset_the_reconnect_budget()
    {
        await using var h = Harness.Create(o => o with
        {
            StableAfter = TimeSpan.FromSeconds(1),
            Reconnect = new ReconnectOptions { MaxAttempts = 1, Delay = _ => TimeSpan.FromMilliseconds(50) },
        });
        await h.Connect();
        await h.WaitEvent("state:Connected");
        await Task.Delay(700);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        await Task.Delay(500);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("disconnected:ReconnectExhausted");
        Assert.Equal(1, h.Count("reconnected"));
    }

    [Fact]
    public async Task A_cancelled_token_sends_nothing()
    {
        await using var h = Harness.Create();
        await h.Connect();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Room.SendMessageAsync("hi", cancelled.Token));
        await Task.Delay(200);
        Assert.Empty(h.Sfu.Current.Messages("send_message"));
    }
}
