using Mezon.Net.Core;

namespace Mezon.Net.Transport.Tests.Tcp;

[Collection("TransportLoopback")]
public class MezonTcpTransporterLifecycleTests
{
    [Fact]
    public async Task Disconnect_InvokesClosed_AndAllowsReconnect()
    {
        await using var server = new Helpers.TcpLoopbackServer();
        var connectionCount = 0;

        server.ClientHandler = async (stream, ct) =>
        {
            Interlocked.Increment(ref connectionCount);
            await Helpers.MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        var closedCount = 0;
        transporter.Closed = _ =>
        {
            Interlocked.Increment(ref closedCount);
            return Task.CompletedTask;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "token-a", useSsl: false).ConfigureAwait(false);
        await transporter.DisconnectAsync().ConfigureAwait(false);

        Assert.Equal(1, closedCount);
        Assert.Equal(ConnectionState.Disconnected, GetConnectionState(transporter));

        await transporter.ConnectAsync("127.0.0.1", server.Port, "token-b", useSsl: false).ConfigureAwait(false);

        var deadline = Environment.TickCount64 + 3000;
        while (connectionCount < 2 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Equal(2, connectionCount);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task FastReconnect_StillReceivesMessages()
    {
        await using var server = new Helpers.TcpLoopbackServer();
        var connectionId = 0;

        server.ClientHandler = async (stream, ct) =>
        {
            var id = Interlocked.Increment(ref connectionId);
            await Helpers.MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(Helpers.MezonTransportFrameBuilder.BuildPongFrame(id), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();

        for (var round = 0; round < 5; round++)
        {
            var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            transporter.MessageReceived = (type, cid, _, _) =>
            {
                if (type == MezonMessageType.Heartbeat)
                {
                    received.TrySetResult(cid);
                }

                return default;
            };

            await transporter.ConnectAsync("127.0.0.1", server.Port, $"token-{round}", useSsl: false).ConfigureAwait(false);
            var pongCid = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert.True(pongCid > 0);
            await transporter.DisconnectAsync().ConfigureAwait(false);
        }

        Assert.Equal(5, connectionId);
    }

    [Fact]
    public async Task Connect_WithoutToken_DoesNotStayConnected()
    {
        await using var server = new Helpers.TcpLoopbackServer();
        server.ClientHandler = async (_, ct) => await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        var errors = 0;
        transporter.ErrorOccurred = _ =>
        {
            Interlocked.Increment(ref errors);
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transporter.ConnectAsync("127.0.0.1", server.Port, token: null, useSsl: false)).ConfigureAwait(false);
        Assert.True(errors >= 1);
        Assert.Equal(ConnectionState.Disconnected, GetConnectionState(transporter));
    }

    [Fact]
    public async Task Connect_failure_does_not_invoke_Closed()
    {
        var transporter = new MezonNetworkTcpTransporter();
        var closedCount = 0;
        transporter.Closed = _ =>
        {
            Interlocked.Increment(ref closedCount);
            return Task.CompletedTask;
        };

        var unusedPort = Helpers.TcpLoopbackServer.ReserveLoopbackPort();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            transporter.ConnectAsync("127.0.0.1", unusedPort, "token", useSsl: false)).ConfigureAwait(false);
        Assert.Equal(0, closedCount);
        Assert.Equal(ConnectionState.Disconnected, GetConnectionState(transporter));
    }

    private static ConnectionState GetConnectionState(MezonNetworkTcpTransporter transporter)
    {
        var field = typeof(MezonNetworkTcpTransporter).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ConnectionState)field!.GetValue(transporter)!;
    }
}
