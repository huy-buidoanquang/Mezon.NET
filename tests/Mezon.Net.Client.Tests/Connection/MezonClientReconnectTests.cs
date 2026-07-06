using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;

namespace Mezon.Net.Client.Tests.Connection;

public sealed class MezonClientReconnectTests
{
    [Fact]
    public async Task Heartbeat_continues_after_auto_reconnect()
    {
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 120);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport).ConfigureAwait(false);
        var client = new MezonClient(options, socketClient);
        client.SetReconnectDelayForTests(80);

        await client.ConnectAsync().ConfigureAwait(false);
        var heartbeatsBeforeDrop = transport.HeartbeatSendCount;
        Assert.True(heartbeatsBeforeDrop >= 1, "Initial connect should perform at least one heartbeat.");

        transport.TriggerClosed();

        var deadline = Environment.TickCount64 + 8000;
        while (Environment.TickCount64 < deadline)
        {
            if (transport.ConnectCount >= 2 && transport.HeartbeatSendCount >= heartbeatsBeforeDrop + 3)
            {
                break;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.True(transport.ConnectCount >= 2, "Expected auto-reconnect after transport drop.");
        Assert.True(
            transport.HeartbeatSendCount >= heartbeatsBeforeDrop + 3,
            $"Expected periodic heartbeats after reconnect; got {transport.HeartbeatSendCount}.");

        await client.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task ConnectAsync_does_not_hang_when_transport_fires_closed_during_connect_failure()
    {
        var transport = new FakeNetworkTransporter();
        transport.ConnectHandler = () =>
        {
            transport.TriggerClosedSynchronously(new Exception("rejected"));
            throw new InvalidOperationException("connect rejected");
        };
        var options = SocketTestDoubles.CreateOptions(transport, connectionTimeoutMs: 2000);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport).ConfigureAwait(false);
        var client = new MezonClient(options, socketClient);
        client.SetReconnectDelayForTests(50_000);

        var connectTask = client.ConnectAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => connectTask.WaitAsync(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        await client.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Reconnecting_fires_after_unexpected_transport_drop()
    {
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 500);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport).ConfigureAwait(false);
        var client = new MezonClient(options, socketClient);
        client.SetReconnectDelayForTests(80);

        var disconnectedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnectingTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += ex => { disconnectedTcs.TrySetResult(ex); return Task.CompletedTask; };
        client.Reconnecting += ex => { reconnectingTcs.TrySetResult(ex); return Task.CompletedTask; };

        await client.ConnectAsync().ConfigureAwait(false);
        transport.TriggerClosed();

        await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        await reconnectingTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        await client.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Reconnecting_does_not_fire_on_user_disconnect()
    {
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 500);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport).ConfigureAwait(false);
        var client = new MezonClient(options, socketClient);

        var disconnectedTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += ex => { disconnectedTcs.TrySetResult(ex); return Task.CompletedTask; };
        var reconnectingFired = false;
        client.Reconnecting += _ => { reconnectingFired = true; return Task.CompletedTask; };

        await client.ConnectAsync().ConfigureAwait(false);
        await client.DisconnectAsync().ConfigureAwait(false);

        await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        Assert.False(reconnectingFired);
    }

    [Fact]
    public async Task WaitAsync_completes_when_connection_state_is_connected()
    {
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport).ConfigureAwait(false);
        var client = new MezonClient(options, socketClient);

        await client.ConnectAsync().ConfigureAwait(false);

        Assert.Equal(ConnectionState.Connected, client.ConnectionState);
        await client.DisconnectAsync().ConfigureAwait(false);
    }
}
