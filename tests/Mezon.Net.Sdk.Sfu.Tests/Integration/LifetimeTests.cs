using System.Net;
using System.Net.Sockets;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

/// <summary>Disposal while connecting, work after a disconnect, and failures of background tasks.</summary>
public class LifetimeTests
{
    [Fact]
    public async Task Dispose_ends_a_connect_waiting_for_the_token_provider()
    {
        var h = Harness.Create();
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = TokenProvider.Create(async (_, cancellationToken) =>
        {
            called.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled.TrySetResult();
                }
            }

            return "";
        });
        var connect = h.Room.ConnectAsync(h.Sfu.Url, provider);
        await called.Task;
        await h.Room.DisposeAsync();
        var finished = await Task.WhenAny(connect, Task.Delay(2000)) == connect;
        await Assert.ThrowsAnyAsync<Exception>(() => connect);
        var providerCancelled = await Task.WhenAny(cancelled.Task, Task.Delay(2000)) == cancelled.Task;
        await h.DisposeAsync();
        Assert.True(finished, "ConnectAsync kept waiting for the token provider after the room was disposed");
        Assert.True(providerCancelled, "the provider's cancellation token was not cancelled");
        Assert.Equal(0, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Dispose_ends_a_connect_waiting_for_the_websocket_handshake()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var h = Harness.Create();
        try
        {
            var accepted = listener.AcceptTcpClientAsync();
            var url = $"ws://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/ws";
            var connect = h.Room.ConnectAsync(url, TokenProvider.Fixed(FakeSfu.Token("7", 3600)));
            using var silent = await accepted;
            await h.Room.DisposeAsync();
            var finished = await Task.WhenAny(connect, Task.Delay(2000)) == connect;
            await Assert.ThrowsAnyAsync<Exception>(() => connect);
            Assert.True(finished, "ConnectAsync kept dialing after the room was disposed");
        }
        finally
        {
            listener.Stop();
            await h.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dispose_closes_a_peer_connection_still_being_created_and_concurrent_disposals_wait()
    {
        var h = Harness.Create();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sfu.CreateGate = gate.Task;
        var connect = h.Connect();
        await h.Wait("the PeerConnection factory", () => h.Sfu.FactoryCalls == 1);
        var first = h.Room.DisposeAsync().AsTask();
        var second = h.Room.DisposeAsync().AsTask();
        await Task.Delay(200);
        Assert.False(first.IsCompleted, "DisposeAsync returned while a PeerConnection was still being created");
        Assert.False(second.IsCompleted, "a concurrent DisposeAsync returned before the first one finished");
        gate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, h.Sfu.PeerCount);
        Assert.True(h.Sfu.Peer.Closed, "the PeerConnection created during DisposeAsync was left open");
        await Assert.ThrowsAnyAsync<Exception>(() => connect);
        await h.DisposeAsync();
    }

    [Fact]
    public async Task The_token_provider_is_not_called_after_disconnect()
    {
        await using var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(30) } });
        h.Sfu.Refuse = true;
        var calls = 0;
        var provider = TokenProvider.Create((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(FakeSfu.Token("7", 30));
        });
        var connect = h.Room.ConnectAsync(h.Sfu.Url, provider);
        await h.Wait("the first attempt", () => Volatile.Read(ref calls) == 1);
        await Task.Delay(300);
        await h.Room.DisconnectAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => connect);
        await Task.Delay(200);
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    private static void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>The exceptions of tasks nobody observed, collected while <paramref name="scenario"/> runs and its garbage is finalized.</summary>
    private static async Task<List<Exception>> UnobservedDuring(Func<Task> scenario)
    {
        Collect();
        var seen = new List<Exception>();
        void Handler(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            lock (seen)
            {
                seen.AddRange(e.Exception.InnerExceptions);
            }
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            await scenario();
            Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }

        lock (seen)
        {
            return seen.ToList();
        }
    }

    [Fact]
    public async Task A_session_failing_before_admission_leaves_no_unobserved_exception()
    {
        Exception? thrown = null;
        var unobserved = await UnobservedDuring(async () =>
        {
            await using var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Enabled = false } });
            h.Sfu.Refuse = true;
            thrown = await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        });
        Assert.NotNull(thrown);
        Assert.DoesNotContain(unobserved, e => ReferenceEquals(e, thrown));
    }

    [Fact]
    public async Task A_failed_push_to_talk_request_is_observed()
    {
        var unobserved = await UnobservedDuring(async () =>
        {
            await using var h = Harness.Create(o => o with { Role = Role.Audience });
            await h.Connect();
            h.Sfu.On("mute", (_, _) => true);
            h.Sfu.On("push_to_talk", (c, _) =>
            {
                c.Send(new { type = "error", message = "invalid_push_to_talk" });
                return true;
            });
            var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetPushToTalkAsync(true));
            Assert.Equal((SfuErrorKind.Timeout, "mute"), (error.Kind, error.Code));
        });
        Assert.DoesNotContain(unobserved, e => e is SfuException { Code: "invalid_push_to_talk" });
    }

    [Fact]
    public async Task An_unexpected_negotiation_failure_ends_the_session_and_is_observed()
    {
        var unobserved = await UnobservedDuring(async () =>
        {
            await using var h = Harness.Create();
            h.Sfu.BrokenPeers = 1;
            await h.Connect();
            await h.Wait("a second session", () => h.Sfu.ConnCount == 2 && h.Room.State == ConnectionState.Connected);
            Assert.True(h.Sfu.Peers[0].Closed, "the failed session's PeerConnection was left open");
            await h.Room.SendMessageAsync("hi");
        });
        Assert.DoesNotContain(unobserved, e => e.Message == FakePeer.BrokenMessage);
    }
}
