using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Transporter;

[Collection("TransportLoopback")]
public class MezonTransporterLifecycleTests
{
    public static IEnumerable<object[]> TransporterKinds() =>
    [
        [TransporterKind.Tcp],
        [TransporterKind.WebSocket],
    ];

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task Closed_handler_can_await_DisconnectAsync_after_server_close(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                await MezonTransportFrameBuilder.ReadHandshakeAsync((NetworkStream)client, ct);
            }

            // Returning closes the connection from the server side.
        });

        var transporter = TransporterFactory.Create(kind);
        var closedHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transporter.Closed = async _ =>
        {
            await transporter.DisconnectAsync();
            closedHandled.TrySetResult();
        };

        await ConnectAsync(transporter, session.Port);

        await closedHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await TransporterFactory.DisposeAsync(transporter);
    }

    [Fact]
    public async Task Server_close_racing_DisconnectAsync_completes_and_raises_Closed_once()
    {
        await using var server = new TcpLoopbackServer();
        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct);
            await Task.Delay(Random.Shared.Next(0, 5), ct);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        var closedCount = 0;
        transporter.Closed = _ =>
        {
            Interlocked.Increment(ref closedCount);
            return Task.CompletedTask;
        };

        for (var i = 0; i < 30; i++)
        {
            await transporter.ConnectAsync("127.0.0.1", server.Port, "token", useSsl: false);
            await Task.Delay(Random.Shared.Next(0, 5));
            await transporter.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));

            var deadline = Environment.TickCount64 + 2000;
            while (Volatile.Read(ref closedCount) < i + 1 && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            Assert.Equal(i + 1, Volatile.Read(ref closedCount));
        }

        await transporter.DisposeAsync();
    }

    [Fact]
    public async Task Tcp_http_401_raises_Closed_with_unauthorized()
    {
        await using var server = new TcpLoopbackServer();
        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n"), ct);
            await stream.FlushAsync(ct);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        transporter.Closed = ex =>
        {
            closed.TrySetResult(ex);
            return Task.CompletedTask;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "expired-token", useSsl: false);

        var reason = await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<NetworkTransportUnauthorizationException>(reason);
        await transporter.DisposeAsync();
    }

    [Fact]
    public async Task WebSocket_close_frame_surfaces_close_code()
    {
        await using var session = await LoopbackSession.StartAsync(TransporterKind.WebSocket, async (client, ct) =>
        {
            var socket = (System.Net.WebSockets.WebSocket)client;
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad token", ct);
        });

        var transporter = new MezonNetworkWebSocketTransporter();
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        transporter.Closed = ex =>
        {
            closed.TrySetResult(ex);
            return Task.CompletedTask;
        };

        await ConnectAsync(transporter, session.Port);

        var reason = await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var socketClosed = Assert.IsType<SocketClosedException>(reason);
        Assert.Equal((int)WebSocketCloseStatus.PolicyViolation, socketClosed.CloseCode);
        await transporter.DisposeAsync();
    }

    [Fact]
    public async Task WebSocket_401_upgrade_throws_unauthorized()
    {
        await using var server = new WebSocketLoopbackServer { RejectStatusCode = 401 };
        server.Start();

        var transporter = new MezonNetworkWebSocketTransporter();
        await Assert.ThrowsAsync<NetworkTransportUnauthorizationException>(() => ConnectAsync(transporter, server.Port));
        await transporter.DisposeAsync();
    }

    [Fact]
    public async Task Cancel_token_aborts_connect_stuck_in_tls_handshake()
    {
        await using var server = new TcpLoopbackServer();
        server.ClientHandler = (_, ct) => Task.Delay(Timeout.Infinite, ct);
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        using var cts = new CancellationTokenSource();
        transporter.SetCancelToken(cts.Token);
        cts.CancelAfter(200);

        var connect = transporter.ConnectAsync("127.0.0.1", server.Port, "token", useSsl: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect.WaitAsync(TimeSpan.FromSeconds(5)));
        await transporter.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SendAsync_racing_DisconnectAsync_only_reports_invalid_operation(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                await MezonTransportFrameBuilder.ReadHandshakeAsync((NetworkStream)client, ct);
            }

            await Task.Delay(Timeout.Infinite, ct);
        });

        var transporter = TransporterFactory.Create(kind);
        await ConnectAsync(transporter, session.Port);

        var sender = Task.Run(async () =>
        {
            for (var i = 0; i < 2_000; i++)
            {
                try
                {
                    await transporter.SendAsync(MezonMessageType.Realtime, 0, new byte[] { 0x08, 0x01 });
                }
                catch (InvalidOperationException)
                {
                }
            }
        });

        await Task.Delay(5);
        await transporter.DisconnectAsync();
        await sender.WaitAsync(TimeSpan.FromSeconds(5));
        await TransporterFactory.DisposeAsync(transporter);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task DisposeAsync_is_idempotent_and_blocks_further_connects(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                await MezonTransportFrameBuilder.ReadHandshakeAsync((NetworkStream)client, ct);
            }

            await Task.Delay(Timeout.Infinite, ct);
        });

        var transporter = TransporterFactory.Create(kind);
        var closedCount = 0;
        transporter.Closed = _ =>
        {
            Interlocked.Increment(ref closedCount);
            return Task.CompletedTask;
        };
        await ConnectAsync(transporter, session.Port);

        await TransporterFactory.DisposeAsync(transporter);
        await TransporterFactory.DisposeAsync(transporter);

        Assert.Equal(1, closedCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => ConnectAsync(transporter, session.Port));
    }

    private static Task ConnectAsync(IMezonNetworkTransporter transporter, int port) =>
        transporter.ConnectAsync("127.0.0.1", port, "token", useSsl: false, createStatus: false);
}
