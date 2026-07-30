using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using Mezon.Net.Core;
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
    public void TryReadFrame_Pong_HighBitCid_IsUnsigned()
    {
        var bytes = MezonTransportFrameBuilder.BuildPongFrame(0x8001);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out _,
            out var cid,
            out _,
            out _));
        Assert.Equal(0x8001, cid);
    }

    [Fact]
    public void TryReadFrame_Api_HighBitCid_IsUnsigned()
    {
        var bytes = MezonTransportFrameBuilder.BuildApiFrame(0x8001, 200, finish: true, [1, 2, 3, 4]);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out var cid,
            out _,
            out _));
        Assert.Equal(MezonMessageType.Api, type);
        Assert.Equal(0x8001, cid);
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
    public void TrimPadding_StripsAllTrailingZeros()
    {
        var frame = new ReadOnlyMemory<byte>([0x0A, 0x0B, 0x0C, 0x00]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal([0x0A, 0x0B, 0x0C], trimmed.ToArray());
    }

    [Fact]
    public void TrimPadding_StripsMoreThanThreeTrailingZeros()
    {
        var frame = new ReadOnlyMemory<byte>([0x01, 0x00, 0x00, 0x00, 0x00]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal([0x01], trimmed.ToArray());
    }

    [Fact]
    public void TryReadFrame_WebSocketBinary_UnwrapsRealtimePayload()
    {
        // 0x82 | len=4 | ABCD
        var bytes = new byte[] { 0x82, 0x04, 0x0A, 0x0B, 0x0C, 0x0D };
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out var cid,
            out var code,
            out var frame));
        Assert.Equal(MezonMessageType.Realtime, type);
        Assert.Equal(-1, cid);
        Assert.Equal(0, code);
        Assert.Equal([0x0A, 0x0B, 0x0C, 0x0D], frame.ToArray());
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void TryReadFrame_WebSocketBinary_ExtendedLength126()
    {
        var payload = new byte[200];
        payload.AsSpan().Fill(0x7E);
        var bytes = new byte[4 + payload.Length];
        bytes[0] = 0x82;
        bytes[1] = 126;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)payload.Length);
        payload.CopyTo(bytes.AsSpan(4));

        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out _,
            out _,
            out var frame));
        Assert.Equal(MezonMessageType.Realtime, type);
        Assert.Equal(payload, frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_WebSocketBinary_DoesNotStripTrailingZeros()
    {
        // WS fanout payloads are not abridged-padded; trailing 0x00 may be meaningful.
        var bytes = new byte[] { 0x82, 0x04, 0x08, 0x00, 0x00, 0x00 };
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out _,
            out _,
            out _,
            out var frame));
        Assert.Equal([0x08, 0x00, 0x00, 0x00], frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_WebSocketBinary_Masked_Throws()
    {
        var buffer = new ReadOnlySequence<byte>(new byte[] { 0x82, 0x84, 0x00, 0x00, 0x00, 0x00, 1, 2, 3, 4 });
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.Throws<InvalidDataException>(() =>
            MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
    }

    [Fact]
    public void TryReadFrame_Abridged_TrimsTrailingZeros()
    {
        var bytes = MezonTransportFrameBuilder.BuildAbridgedFrame([0x0A, 0x0B, 0x0C]);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out _,
            out _,
            out var frame));
        Assert.Equal(MezonMessageType.Realtime, type);
        Assert.Equal([0x0A, 0x0B, 0x0C], frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_UnexpectedLeadByte_Throws()
    {
        var buffer = new ReadOnlySequence<byte>(new byte[] { 0x80, 0x00, 0x00, 0x00 });
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.Throws<InvalidDataException>(() =>
            MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
    }

    [Fact]
    public void TryReadFrame_OversizedAbridged_Throws()
    {
        // Extended header claiming payload larger than MaxAbridgedReceiveFrameLen.
        var header = new byte[] { 0x7f, 0x00, 0x08, 0x00 }; // lenDiv4 = 0x800 → payload = 8192, total = 8196
        var buffer = new ReadOnlySequence<byte>(header);
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.Throws<InvalidDataException>(() =>
            MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
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
    public void TryQueueRealtimeFrame_ExactMaxSize_Succeeds()
    {
        // Extended header (4) + payload → total 4096 ⇒ payloadWithPadding = 4092 ⇒ payload = 4092.
        var payload = new byte[4092];
        payload.AsSpan().Fill(0xAB);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        Assert.True(MezonTransportFrameCodec.TryQueueRealtimeFrame(channel.Writer, payload));
        Assert.True(channel.Reader.TryRead(out var frame));
        Assert.Equal(MezonTransportFrameCodec.MaxAbridgedSendFrameLen, frame.Length);
        MezonTransportFrameCodec.ReturnPooledSendBuffer(frame);
    }

    [Fact]
    public void TryQueueRealtimeFrame_OverMaxSize_Throws()
    {
        var payload = new byte[4093];
        payload.AsSpan().Fill(0xAB);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        var ex = Assert.Throws<NetworkTransportPayloadTooLargeException>(() =>
            MezonTransportFrameCodec.TryQueueRealtimeFrame(channel.Writer, payload));
        Assert.True(ex.FrameSize > MezonTransportFrameCodec.MaxAbridgedSendFrameLen);
        Assert.Equal(MezonTransportFrameCodec.MaxAbridgedSendFrameLen, ex.MaxFrameSize);
        Assert.False(channel.Reader.TryRead(out _));
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
