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
        Assert.Equal(2, connectionCount);
        await transporter.DisconnectAsync().ConfigureAwait(false);
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

    private static ConnectionState GetConnectionState(MezonNetworkTcpTransporter transporter)
    {
        var field = typeof(MezonNetworkTcpTransporter).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ConnectionState)field!.GetValue(transporter)!;
    }
}
