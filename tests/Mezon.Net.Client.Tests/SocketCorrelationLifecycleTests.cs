using System.Collections.Concurrent;
using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;

namespace Mezon.Net.Client.Tests;

public sealed class SocketCorrelationLifecycleTests
{
    [Fact]
    public void RegisterNext_cids_are_unique_across_wraparound_under_concurrency()
    {
        var hub = new SocketCorrelationHub(counter: ushort.MaxValue - 100);
        var cids = new ConcurrentBag<int>();

        Parallel.For(0, 20_000, _ => cids.Add(hub.RegisterNext().Cid));

        Assert.Equal(20_000, cids.Distinct().Count());
        Assert.All(cids, cid => Assert.InRange(cid, 1, ushort.MaxValue));
        Assert.Equal(20_000, hub.PendingCount);
    }

    [Fact]
    public void RegisterNext_throws_when_every_cid_is_pending()
    {
        var hub = new SocketCorrelationHub();
        for (var i = 0; i < ushort.MaxValue; i++)
        {
            hub.RegisterNext();
        }

        Assert.Throws<InvalidOperationException>(() => hub.RegisterNext());
        Assert.Throws<InvalidOperationException>(() => hub.AllocateCid());
    }

    [Fact]
    public async Task Completed_request_releases_timeout_timer()
    {
        var hub = new SocketCorrelationHub();
        var pending = hub.RegisterNext();
        pending.StartTimeout(60_000);
        Assert.True(pending.HasTimeoutTimer);

        Assert.True(hub.TryComplete(pending.Cid, 0, new byte[] { 1 }));

        Assert.False(pending.HasTimeoutTimer);
        var response = await pending.Task;
        Assert.Equal(1, response.Payload.Length);
    }

    [Fact]
    public async Task StartTimeout_after_completion_does_not_create_timer()
    {
        var hub = new SocketCorrelationHub();
        var pending = hub.RegisterNext();
        Assert.True(hub.TryComplete(pending.Cid, 0, ReadOnlyMemory<byte>.Empty));

        pending.StartTimeout(50);

        Assert.False(pending.HasTimeoutTimer);
        var response = await pending.Task;
        Assert.Equal(0, response.Code);
    }

    [Fact]
    public async Task Server_close_fails_pending_request_without_waiting_for_timeout()
    {
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 60_000);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var client = new MezonClient(options, socketClient);
        client.SetReconnectDelayForTests(60_000);
        await client.ConnectAsync();

        transport.AutoRespondToHeartbeat = false;
        var heartbeat = socketClient.Heartbeat(new RequestOptions { SocketSendTimeout = 30_000 });
        Assert.Equal(1, socketClient.PendingSocketRequestCount);

        transport.TriggerClosed();

        var completed = await Task.WhenAny(heartbeat, Task.Delay(2_000));
        Assert.Same(heartbeat, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => heartbeat);
        Assert.Equal(0, socketClient.PendingSocketRequestCount);

        await client.DisconnectAsync();
    }
}
