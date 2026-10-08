using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;

namespace Mezon.Net.Client.Tests;

public sealed class HeartbeatLatencyTests
{
    private const int PongDelayMs = 50;

    // Timers can fire up to one coarse clock tick early, so only half the pong delay is guaranteed.
    private const int MinExpectedLatencyMs = PongDelayMs / 2;

    [Theory]
    [InlineData(TransportType.WebSocket)]
    [InlineData(TransportType.Tcp)]
    public async Task Heartbeat_records_round_trip_latency(TransportType transportType)
    {
        var transport = new FakeNetworkTransporter { PongDelayMilliseconds = PongDelayMs };
        var options = SocketTestDoubles.CreateOptions(transport, transportType: transportType);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        await socketClient.ConnectAsync();

        await socketClient.Heartbeat(new RequestOptions { SocketSendTimeout = 5_000 });

        Assert.Equal(1, transport.HeartbeatSendCount);
        Assert.InRange(socketClient.LatencyMilliseconds, MinExpectedLatencyMs, 5_000);
        Assert.Equal(0, socketClient.PendingSocketRequestCount);
        await socketClient.DisconnectAsync();
    }

    [Theory]
    [InlineData(TransportType.WebSocket)]
    [InlineData(TransportType.Tcp)]
    public async Task Client_latency_is_set_by_the_heartbeat_loop(TransportType transportType)
    {
        var transport = new FakeNetworkTransporter { PongDelayMilliseconds = PongDelayMs };
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 1_000, transportType: transportType);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var client = new MezonClient(options, socketClient);
        client.SetReconnectDelayForTests(60_000);
        await client.ConnectAsync();

        // The loop sends its first heartbeat as soon as the socket connects.
        var deadline = Environment.TickCount64 + 5_000;
        while (client.Latency == 0 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
        }

        Assert.InRange(client.Latency, MinExpectedLatencyMs, 5_000);
        await client.DisconnectAsync();
    }

    [Theory]
    [InlineData(TransportType.WebSocket)]
    [InlineData(TransportType.Tcp)]
    public async Task Unanswered_heartbeat_does_not_record_latency(TransportType transportType)
    {
        var transport = new FakeNetworkTransporter { AutoRespondToHeartbeat = false };
        var options = SocketTestDoubles.CreateOptions(transport, transportType: transportType);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        await socketClient.ConnectAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => socketClient.Heartbeat(new RequestOptions { SocketSendTimeout = 50 }));

        Assert.Equal(0, socketClient.LatencyMilliseconds);
        await socketClient.DisconnectAsync();
    }
}
