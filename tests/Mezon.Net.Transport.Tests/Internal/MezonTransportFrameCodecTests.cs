using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net.WebSockets;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport.Internal;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Internal;

public class MezonTransportFrameCodecTests
{
    [Fact]
    public void TryReadFrame_Pong_ParsesCid()
    {
        var bytes = MezonTransportFrameBuilder.BuildPongFrame(99);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out var cid,
            out var code,
            out _));
        Assert.Equal(MezonMessageType.Heartbeat, type);
        Assert.Equal(99, cid);
        Assert.Equal(0, code);
    }

    [Fact]
    public void TryReadFrame_ApiChunked_ReassemblesPayload()
    {
        var bytes = MezonTransportFrameBuilder.BuildApiFrame(3, 201, finish: false, [1, 2])
            .Concat(MezonTransportFrameBuilder.BuildApiFrame(3, 201, finish: true, [3, 4]))
            .ToArray();
        var buffer = new ReadOnlySequence<byte>(bytes);
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.False(MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
        Assert.True(MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out var type, out var cid, out var code, out var frame));
        Assert.Equal(MezonMessageType.Api, type);
        Assert.Equal(3, cid);
        Assert.Equal(201, code);
        Assert.Equal([1, 2, 3, 4], frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_ApiSplitHeaderAndPayload_DoesNotDesync()
    {
        var payload = new byte[66];
        payload.AsSpan().Fill(0xAB);
        var full = MezonTransportFrameBuilder.BuildApiFrame(5, 0, finish: true, payload);
        var headerOnly = full.AsSpan(0, 11).ToArray();
        var rest = full.AsSpan(11).ToArray();

        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        var buffer = new ReadOnlySequence<byte>(headerOnly);
        Assert.False(MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
        Assert.True(buffer.Length > 0);

        buffer = new ReadOnlySequence<byte>(headerOnly.Concat(rest).ToArray());
        Assert.True(MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out var type, out var cid, out _, out var frame));
        Assert.Equal(MezonMessageType.Api, type);
        Assert.Equal(5, cid);
        Assert.Equal(payload, frame.ToArray());
    }

    [Fact]
    public void TrimPadding_RemovesUpToThreeTrailingZeros()
    {
        var frame = new ReadOnlyMemory<byte>([0x0A, 0x0B, 0x0C, 0x00]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal([0x0A, 0x0B, 0x0C], trimmed.ToArray());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(20)]
    public void TryQueueAbridgedFrame_WritesLengthPrefix(int payloadLength)
    {
        var payload = new byte[payloadLength];
        payload.AsSpan().Fill(0xAB);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        Assert.True(MezonTransportFrameCodec.TryQueueRealtimeFrame(channel.Writer, payload));
        Assert.True(channel.Reader.TryRead(out var frame));
        int padding = (4 - (payloadLength % 4)) & 3;
        int totalPayload = payloadLength + padding;
        int lenDiv4 = totalPayload / 4;
        Assert.Equal(lenDiv4 < 127 ? 1 : 4, frame.Length - totalPayload);
        MezonTransportFrameCodec.ReturnPooledSendBuffer(frame);
    }

    [Fact]
    public void TryQueuePingFrame_WritesThreeByteFrame()
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        Assert.True(MezonTransportFrameCodec.TryQueuePingFrame(channel.Writer, 12));
        Assert.True(channel.Reader.TryRead(out var frame));
        Assert.Equal(3, frame.Length);
        Assert.Equal(0x00, frame.Span[0]);
        Assert.Equal(12, BinaryPrimitives.ReadUInt16BigEndian(frame.Span.Slice(1)));
        MezonTransportFrameCodec.ReturnPooledSendBuffer(frame);
    }
}
