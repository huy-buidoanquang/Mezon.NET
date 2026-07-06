using System.Buffers;
using System.Collections.Concurrent;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Transport.Internal;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Internal;

public class MezonWebSocketFrameCodecTests
{
    [Fact]
    public void TryHandleMessage_RawEnvelope_ReturnsAbridgedPayload()
    {
        var payload = new byte[] { 0x0A, 0x0B, 0x0C };
        Assert.True(MezonWebSocketFrameCodec.TryHandleMessage(
            payload,
            new ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out var cid,
            out var code,
            out var frame));
        Assert.Equal(MezonMessageType.Abridged, type);
        Assert.Equal(0, cid);
        Assert.Equal(0, code);
        Assert.Equal(payload, frame.ToArray());
    }

    [Fact]
    public void TryHandleMessage_ApiChunks_ReassemblesPayload()
    {
        var apiChunkBuffers = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        var first = MezonTransportFrameBuilder.BuildWebSocketApiFrame(5, 200, finish: false, [1, 2]);
        Assert.False(MezonWebSocketFrameCodec.TryHandleMessage(first, apiChunkBuffers, out _, out _, out _, out _));

        var second = MezonTransportFrameBuilder.BuildWebSocketApiFrame(5, 200, finish: true, [3, 4]);
        Assert.True(MezonWebSocketFrameCodec.TryHandleMessage(second, apiChunkBuffers, out var type, out var cid, out var code, out var frame));
        Assert.Equal(MezonMessageType.Api, type);
        Assert.Equal(5, cid);
        Assert.Equal(200, code);
        Assert.Equal([1, 2, 3, 4], frame.ToArray());
    }

    [Fact]
    public void TryQueueRawFrame_WritesExactPayload()
    {
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        Assert.True(MezonWebSocketFrameCodec.TryQueueRawFrame(channel.Writer, payload));
        Assert.True(channel.Reader.TryRead(out var frame));
        Assert.Equal(payload, frame.ToArray());
        MezonWebSocketFrameCodec.ReturnPooledSendBuffer(frame);
    }
}
