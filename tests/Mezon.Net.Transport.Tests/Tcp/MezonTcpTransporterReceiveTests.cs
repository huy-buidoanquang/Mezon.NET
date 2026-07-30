using System.Collections.Concurrent;
using Mezon.Net.Core;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Tcp;

[Collection("TransportLoopback")]
public class MezonTcpTransporterReceiveTests
{
    [Fact]
    public async Task Receive_PongFrame_ParsesCid()
    {
        await using var server = new TcpLoopbackServer();
        var received = new TaskCompletionSource<(MezonMessageType type, int cid)>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(MezonTransportFrameBuilder.BuildPongFrame(42), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.MessageReceived = (type, cid, code, _) =>
        {
            if (type == MezonMessageType.Heartbeat)
            {
                received.TrySetResult((type, cid));
            }

            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var (messageType, cid) = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.Equal(MezonMessageType.Heartbeat, messageType);
        Assert.Equal(42, cid);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_ApiFrame_ChunkedReassemblesPayload()
    {
        await using var server = new TcpLoopbackServer();
        var received = new TaskCompletionSource<(int cid, int code, byte[] payload)>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(MezonTransportFrameBuilder.BuildApiFrame(7, 200, finish: false, [1, 2]), ct).ConfigureAwait(false);
            await stream.WriteAsync(MezonTransportFrameBuilder.BuildApiFrame(7, 200, finish: true, [3, 4]), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.MessageReceived = (type, cid, code, data) =>
        {
            if (type == MezonMessageType.Api)
            {
                received.TrySetResult((cid, code, data.ToArray()));
            }

            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.Equal(7, result.cid);
        Assert.Equal(200, result.code);
        Assert.Equal([1, 2, 3, 4], result.payload);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_AbridgedFrame_TrimsPadding()
    {
        await using var server = new TcpLoopbackServer();
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(MezonTransportFrameBuilder.BuildAbridgedFrame([0x0A, 0x0B, 0x0C]), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.MessageReceived = (type, _, _, data) =>
        {
            if (type == MezonMessageType.Realtime)
            {
                received.TrySetResult(data.ToArray());
            }

            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.Equal([0x0A, 0x0B, 0x0C], payload);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_MultipleFrames_InSingleRead_DispatchesAll()
    {
        await using var server = new TcpLoopbackServer();
        var messages = new ConcurrentQueue<(MezonMessageType type, int cid)>();

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            var batch = MezonTransportFrameBuilder.BuildPongFrame(1)
                .Concat(MezonTransportFrameBuilder.BuildPongFrame(2))
                .ToArray();
            await stream.WriteAsync(batch, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.MessageReceived = (type, cid, _, _) =>
        {
            messages.Enqueue((type, cid));
            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        await Task.Delay(500).ConfigureAwait(false);

        var list = messages.ToList();
        Assert.Contains((MezonMessageType.Heartbeat, 1), list);
        Assert.Contains((MezonMessageType.Heartbeat, 2), list);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_WebSocketBinaryFrame_DeliversRealtimeWithoutDisconnect()
    {
        await using var server = new TcpLoopbackServer();
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            // 0x82 | len=3 | payload (envelope-ish bytes)
            await stream.WriteAsync(new byte[] { 0x82, 0x03, 0x08, 0x01, 0x00 }, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        var errorFired = false;
        transporter.ErrorOccurred = _ =>
        {
            errorFired = true;
            return Task.CompletedTask;
        };
        transporter.MessageReceived = (type, _, _, data) =>
        {
            if (type == MezonMessageType.Realtime)
            {
                received.TrySetResult(data.ToArray());
            }

            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.Equal([0x08, 0x01, 0x00], payload);
        Assert.False(errorFired);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_UnexpectedLeadByte_FiresErrorAndDisconnects()
    {
        await using var server = new TcpLoopbackServer();
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(new byte[] { 0x80, 0x00, 0x00, 0x00 }, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.ErrorOccurred = ex =>
        {
            error.TrySetResult(ex);
            return Task.CompletedTask;
        };
        transporter.Closed = _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var ex = await error.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsType<InvalidDataException>(ex);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    [Fact]
    public async Task Receive_Pong_HighBitCid_IsUnsigned()
    {
        await using var server = new TcpLoopbackServer();
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            await stream.WriteAsync(MezonTransportFrameBuilder.BuildPongFrame(0x8001), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        transporter.MessageReceived = (type, cid, _, _) =>
        {
            if (type == MezonMessageType.Heartbeat)
            {
                received.TrySetResult(cid);
            }

            return default;
        };

        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        var cid = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.Equal(0x8001, cid);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }
}
