using Mezon.Net.Core;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Tcp;

[Collection("TransportLoopback")]
public class MezonTcpTransporterSendTests
{
    [Fact]
    public async Task Send_Heartbeat_WritesPongFrame()
    {
        await using var server = new TcpLoopbackServer();
        var pingReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            var buffer = new byte[3];
            await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
            pingReceived.TrySetResult(buffer);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        await transporter.SendAsync(MezonMessageType.Heartbeat, 9, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);

        var frame = await pingReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.Equal([0x00, 0x00, 0x09], frame);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Send_Abridged_WritesLengthPrefixedFrame()
    {
        await using var server = new TcpLoopbackServer();
        var frameReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientHandler = async (stream, ct) =>
        {
            await MezonTransportFrameBuilder.ReadHandshakeAsync(stream, ct).ConfigureAwait(false);
            var header = new byte[1];
            await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
            Assert.Equal(1, header[0]);
            var payload = new byte[4];
            await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
            frameReceived.TrySetResult(header.Concat(payload).ToArray());
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        };
        server.Start();

        var transporter = new MezonNetworkTcpTransporter();
        await transporter.ConnectAsync("127.0.0.1", server.Port, "test-token", useSsl: false).ConfigureAwait(false);
        await transporter.SendAsync(MezonMessageType.Abridged, 0, new ReadOnlyMemory<byte>(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })).ConfigureAwait(false);

        var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.Equal(0x01, frame[0]);
        Assert.Equal(0xDE, frame[1]);
        Assert.Equal(0xAD, frame[2]);
        Assert.Equal(0xBE, frame[3]);
        Assert.Equal(0xEF, frame[4]);
        await transporter.DisconnectAsync().ConfigureAwait(false);
    }
}
