using System.Net.WebSockets;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Transport;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Dev;

[Collection("TransportLoopback")]
public sealed class DevTransportHeartbeatFrameTests
{
    [Fact]
    public void CreateHeartbeatFrame_Tcp_ReturnsEmptyHeartbeat()
    {
        var (type, payload) = DevTransportIntegrationTests.CreateHeartbeatFrame(TransporterKind.Tcp, 7);

        Assert.Equal(MezonMessageType.Heartbeat, type);
        Assert.True(payload.IsEmpty);
    }

    [Fact]
    public void CreateHeartbeatFrame_WebSocket_ReturnsRealtimePingEnvelope()
    {
        var (type, payload) = DevTransportIntegrationTests.CreateHeartbeatFrame(TransporterKind.WebSocket, 42);

        Assert.Equal(MezonMessageType.Realtime, type);
        var envelope = Envelope.Parser.ParseFrom(payload.Span);
        Assert.Equal(42, envelope.Cid);
        Assert.Equal(Envelope.MessageOneofCase.Ping, envelope.MessageCase);
    }

    [Fact]
    public void CreateHeartbeatFrame_InvalidKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DevTransportIntegrationTests.CreateHeartbeatFrame((TransporterKind)999, 1));
    }

    [Fact]
    public void IsHeartbeatResponse_ValidatesTcpAndWebSocketFrames()
    {
        var pongMatch = new Envelope { Cid = 5, Pong = new Pong() }.ToByteArray();
        var pongOtherCid = new Envelope { Cid = 6, Pong = new Pong() }.ToByteArray();
        var pingEnvelope = new Envelope { Cid = 5, Ping = new Ping() }.ToByteArray();
        byte[] invalidProto = [0xFF, 0xFF, 0xFF];

        Assert.True(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.Tcp, 5, MezonMessageType.Heartbeat, 5, ReadOnlySpan<byte>.Empty));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.Tcp, 5, MezonMessageType.Heartbeat, 6, ReadOnlySpan<byte>.Empty));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.Tcp, 5, MezonMessageType.Realtime, 5, ReadOnlySpan<byte>.Empty));

        Assert.True(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.WebSocket, 5, MezonMessageType.Realtime, 0, pongMatch));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.WebSocket, 5, MezonMessageType.Realtime, 0, pongOtherCid));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.WebSocket, 5, MezonMessageType.Realtime, 0, pingEnvelope));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.WebSocket, 5, MezonMessageType.Realtime, 0, invalidProto));
        Assert.False(DevTransportIntegrationTests.IsHeartbeatResponse(TransporterKind.WebSocket, 5, MezonMessageType.Heartbeat, 5, pongMatch));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DevTransportIntegrationTests.IsHeartbeatResponse((TransporterKind)999, 1, MezonMessageType.Heartbeat, 1, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public async Task WebSocket_HeartbeatFrame_RoundTripsThroughLoopbackServer()
    {
        await using var server = new WebSocketLoopbackServer();
        server.ClientHandler = async (socket, ct) =>
        {
            var received = await WebSocketLoopbackServer.ReadBinaryMessageAsync(socket, ct).ConfigureAwait(false);
            var envelope = Envelope.Parser.ParseFrom(received);
            Assert.Equal(Envelope.MessageOneofCase.Ping, envelope.MessageCase);
            var pong = new Envelope { Cid = envelope.Cid, Pong = new Pong() }.ToByteArray();
            await socket.SendAsync(new ArraySegment<byte>(pong), WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
            await Task.Delay(200, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkWebSocketTransporter();
        var events = new TransporterEventCapture();
        events.Attach(transporter);

        try
        {
            await transporter.ConnectAsync("127.0.0.1", server.Port, "tok", useSsl: false, createStatus: false).ConfigureAwait(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                transporter.SendAsync(MezonMessageType.Heartbeat, 1, ReadOnlyMemory<byte>.Empty).AsTask()).ConfigureAwait(false);

            var (type, payload) = DevTransportIntegrationTests.CreateHeartbeatFrame(TransporterKind.WebSocket, 9);
            await transporter.SendAsync(type, 9, payload).ConfigureAwait(false);

            var response = await events.WaitForMessageAsync(null, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert.True(DevTransportIntegrationTests.IsHeartbeatResponse(
                TransporterKind.WebSocket, 9, response.type, response.cid, response.payload));
        }
        finally
        {
            await DevTransportIntegrationTests.SafeCloseAndDisposeAsync(transporter).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task SafeCloseAndDisposeAsync_DisposesEvenWhenDisconnectThrows()
    {
        var transporter = new ThrowingDisconnectTransporter();

        await DevTransportIntegrationTests.SafeCloseAndDisposeAsync(transporter).ConfigureAwait(false);

        Assert.True(transporter.DisconnectCalled);
        Assert.True(transporter.Disposed);
    }

    private sealed class ThrowingDisconnectTransporter : IMezonNetworkTransporter
    {
        public bool DisconnectCalled { get; private set; }
        public bool Disposed { get; private set; }

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        public void SetHeader(IDictionary<string, string> headers) { }
        public void SetCancelToken(CancellationToken cancellationToken) { }
        public Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false) => Task.CompletedTask;
        public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data) => default;

        public Task DisconnectAsync(int closeCode = 1000, string? reason = null)
        {
            DisconnectCalled = true;
            throw new InvalidOperationException("disconnect boom");
        }

        public void Dispose() => Disposed = true;
    }
}
