using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Transporter;

[Collection("TransportLoopback")]
public class MezonTransporterContractTests
{
    public static IEnumerable<object[]> TransporterKinds() =>
    [
        [TransporterKind.Tcp],
        [TransporterKind.WebSocket],
    ];

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task Connect_InvokesOpened_Event(TransporterKind kind)
    {
        await using var session = await StartIdleServerAsync(kind).ConfigureAwait(false);
        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        await ConnectAsync(transporter, session.Port, "token-opened").ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);

        Assert.Equal(1, events.OpenedCount);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task Disconnect_InvokesClosed_Event(TransporterKind kind)
    {
        await using var session = await StartIdleServerAsync(kind).ConfigureAwait(false);
        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        await ConnectAsync(transporter, session.Port, "token-closed").ConfigureAwait(false);
        await transporter.DisconnectAsync().ConfigureAwait(false);

        Assert.Equal(1, events.ClosedCount);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SetCancelToken_CancelsInFlightConnect(TransporterKind kind)
    {
        await using var session = await StartIdleServerAsync(kind).ConfigureAwait(false);
        var transporter = TransporterFactory.Create(kind);
        using var cts = new CancellationTokenSource();
        transporter.SetCancelToken(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ConnectAsync(transporter, session.Port, "cancel-token")).ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task ErrorOccurred_FiresOnUnauthorizedConnect(TransporterKind kind)
    {
        await using var session = await StartIdleServerAsync(kind).ConfigureAwait(false);
        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            ConnectAsync(transporter, session.Port, token: null)).ConfigureAwait(false);

        Assert.True(events.ErrorCount >= 1);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SetHeader_Token_AllowsConnectWithoutExplicitToken(TransporterKind kind)
    {
        await using var session = await StartIdleServerAsync(kind).ConfigureAwait(false);
        var transporter = TransporterFactory.Create(kind);
        transporter.SetHeader(new Dictionary<string, string> { ["token"] = "header-token" });
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        await ConnectAsync(transporter, session.Port, token: null).ConfigureAwait(false);
        Assert.Equal(1, events.OpenedCount);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task MessageReceived_DispatchesHeartbeat(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                var stream = (NetworkStream)client;
                await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
                await WriteToClientAsync(kind, client, MezonTransportFrameBuilder.BuildPongFrame(42), ct).ConfigureAwait(false);
            }
            else
            {
                var pong = new Envelope { Cid = 42, Pong = new Pong() };
                await WriteToClientAsync(kind, client, pong.ToByteArray(), ct).ConfigureAwait(false);
            }

            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);
        await ConnectAsync(transporter, session.Port, "token-heartbeat").ConfigureAwait(false);

        if (kind == TransporterKind.Tcp)
        {
            var message = await events.WaitForMessageAsync(m => m.type == MezonMessageType.Heartbeat).ConfigureAwait(false);
            Assert.Equal(42, message.cid);
        }
        else
        {
            var message = await events.WaitForMessageAsync(m => m.type == MezonMessageType.Realtime).ConfigureAwait(false);
            var envelope = Envelope.Parser.ParseFrom(message.payload);
            Assert.NotNull(envelope.Pong);
            Assert.Equal(42, envelope.Cid);
        }

        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task MessageReceived_ReassemblesApiChunks(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                var stream = (NetworkStream)client;
                await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
                var batch = MezonTransportFrameBuilder.BuildApiFrame(7, 200, finish: false, [1, 2])
                    .Concat(MezonTransportFrameBuilder.BuildApiFrame(7, 200, finish: true, [3, 4]))
                    .ToArray();
                await WriteToClientAsync(kind, client, batch, ct).ConfigureAwait(false);
            }
            else
            {
                await WriteToClientAsync(kind, client, MezonTransportFrameBuilder.BuildWebSocketApiFrame(7, 200, finish: false, [1, 2]), ct).ConfigureAwait(false);
                await WriteToClientAsync(kind, client, MezonTransportFrameBuilder.BuildWebSocketApiFrame(7, 200, finish: true, [3, 4]), ct).ConfigureAwait(false);
            }
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);
        await ConnectAsync(transporter, session.Port, "token-api").ConfigureAwait(false);

        var message = await events.WaitForMessageAsync(m => m.type == MezonMessageType.Api).ConfigureAwait(false);
        Assert.Equal(7, message.cid);
        Assert.Equal(200, message.code);
        Assert.Equal([1, 2, 3, 4], message.payload);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task MessageReceived_TrimsAbridgedPadding(TransporterKind kind)
    {
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                var stream = (NetworkStream)client;
                await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            }

            if (kind == TransporterKind.Tcp)
            {
                await WriteToClientAsync(kind, client, MezonTransportFrameBuilder.BuildAbridgedFrame([0x0A, 0x0B, 0x0C]), ct).ConfigureAwait(false);
            }
            else
            {
                await WriteToClientAsync(kind, client, [0x0A, 0x0B, 0x0C], ct).ConfigureAwait(false);
            }
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);
        await ConnectAsync(transporter, session.Port, "token-abridged").ConfigureAwait(false);

        var message = await events.WaitForMessageAsync(m => m.type == MezonMessageType.Realtime).ConfigureAwait(false);
        Assert.Equal([0x0A, 0x0B, 0x0C], message.payload);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SendAsync_Heartbeat_WritesPongFrame(TransporterKind kind)
    {
        var frameReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                var stream = (NetworkStream)client;
                await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
                var buffer = new byte[3];
                await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
                frameReceived.TrySetResult(buffer);
            }
            else
            {
                var socket = (System.Net.WebSockets.WebSocket)client;
                var frame = await WebSocketLoopbackServer.ReadBinaryMessageAsync(socket, ct).ConfigureAwait(false);
                frameReceived.TrySetResult(frame);
            }

            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        await ConnectAsync(transporter, session.Port, "token-send-ping").ConfigureAwait(false);
        if (kind == TransporterKind.Tcp)
        {
            await transporter.SendAsync(MezonMessageType.Heartbeat, 9, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
            var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert.Equal([0x00, 0x00, 0x09], frame);
        }
        else
        {
            var ping = new Envelope { Cid = 9, Ping = new Ping() };
            await transporter.SendAsync(MezonMessageType.Realtime, 9, ping.ToByteArray()).ConfigureAwait(false);
            var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert.Equal(ping.ToByteArray(), frame);
        }

        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SendAsync_Abridged_WritesLengthPrefixedPayload(TransporterKind kind)
    {
        var frameReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                var stream = (NetworkStream)client;
                await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
                var header = new byte[1];
                await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
                var payload = new byte[4];
                await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
                frameReceived.TrySetResult(header.Concat(payload).ToArray());
            }
            else
            {
                var socket = (System.Net.WebSockets.WebSocket)client;
                frameReceived.TrySetResult(await WebSocketLoopbackServer.ReadBinaryMessageAsync(socket, ct).ConfigureAwait(false));
            }

            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        await ConnectAsync(transporter, session.Port, "token-send-abridged").ConfigureAwait(false);
        await transporter.SendAsync(MezonMessageType.Realtime, 0, new ReadOnlyMemory<byte>(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })).ConfigureAwait(false);

        var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (kind == TransporterKind.Tcp)
        {
            Assert.Equal(0x01, frame[0]);
            Assert.Equal(0xDE, frame[1]);
            Assert.Equal(0xAD, frame[2]);
            Assert.Equal(0xBE, frame[3]);
            Assert.Equal(0xEF, frame[4]);
        }
        else
        {
            Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], frame);
        }
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task SendAsync_WhenDisconnected_ReturnsWithoutThrowing(TransporterKind kind)
    {
        var transporter = TransporterFactory.Create(kind);
        await transporter.SendAsync(MezonMessageType.Heartbeat, 1, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
        await transporter.SendAsync(MezonMessageType.Realtime, 0, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
        await transporter.SendAsync(MezonMessageType.Api, 0, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TransporterKinds))]
    public async Task Disconnect_AllowsReconnect(TransporterKind kind)
    {
        var connectionCount = 0;
        await using var session = await LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            Interlocked.Increment(ref connectionCount);
            if (kind == TransporterKind.Tcp)
            {
                await MezonTransportFrameBuilder.ReadHandshakeAsync((NetworkStream)client, ct).ConfigureAwait(false);
            }

            await HoldConnectionOpenAsync(kind, client, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var transporter = TransporterFactory.Create(kind);
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        await ConnectAsync(transporter, session.Port, "token-a").ConfigureAwait(false);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await Task.Delay(kind == TransporterKind.WebSocket ? 500 : 200).ConfigureAwait(false);

        await ConnectAsync(transporter, session.Port, "token-b").ConfigureAwait(false);

        var deadline = Environment.TickCount64 + 3000;
        while (connectionCount < 2 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Equal(2, connectionCount);
        Assert.Equal(2, events.OpenedCount);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

    private static async Task HoldConnectionOpenAsync(TransporterKind kind, object client, CancellationToken ct)
    {
        if (kind == TransporterKind.Tcp)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return;
        }

        var socket = (System.Net.WebSockets.WebSocket)client;
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    private static Task<LoopbackSession> StartIdleServerAsync(TransporterKind kind) =>
        LoopbackSession.StartAsync(kind, async (client, ct) =>
        {
            if (kind == TransporterKind.Tcp)
            {
                await MezonTransportFrameBuilder.ReadHandshakeAsync((NetworkStream)client, ct).ConfigureAwait(false);
            }

            await HoldConnectionOpenAsync(kind, client, ct).ConfigureAwait(false);
        });

    private static Task ConnectAsync(IMezonNetworkTransporter transporter, int port, string? token) =>
        transporter.ConnectAsync("127.0.0.1", port, token, useSsl: false, createStatus: false);

    private static async Task WriteToClientAsync(TransporterKind kind, object client, byte[] frame, CancellationToken ct)
    {
        if (kind == TransporterKind.Tcp)
        {
            var stream = (NetworkStream)client;
            await stream.WriteAsync(frame, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return;
        }

        var socket = (System.Net.WebSockets.WebSocket)client;
        await socket.SendAsync(frame, WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
    }
}
