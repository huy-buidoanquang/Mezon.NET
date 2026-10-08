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

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(MezonTransportFrameCodec.MaxApiResponseLen + 1)]
    public void TryReadFrame_ApiLengthOutsideCap_Throws(int payloadLen)
    {
        var header = new byte[11];
        header[0] = 0xff;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(1), 9);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(3), 0xff);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(7), payloadLen);
        var buffer = new ReadOnlySequence<byte>(header);
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.Throws<InvalidDataException>(() =>
            MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out _));
    }

    [Fact]
    public void TryReadFrame_SingleFinishedApiChunk_IsExactSizeCopy()
    {
        var bytes = MezonTransportFrameBuilder.BuildApiFrame(4, 0, finish: true, [1, 2, 3, 4, 5]);
        var buffer = new ReadOnlySequence<byte>(bytes);
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        Assert.True(MezonTransportFrameCodec.TryReadFrame(ref buffer, apiChunkBuffers, out _, out _, out _, out var frame));

        Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(frame, out var segment));
        Assert.Equal(5, segment.Array!.Length);
        Assert.NotSame(bytes, segment.Array);
        Assert.Empty(apiChunkBuffers);
    }

    [Fact]
    public void AppendApiChunk_OverCumulativeCap_CompletesWithTooLargeCode()
    {
        var apiChunkBuffers = new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        var code = 0;
        var first = new ReadOnlySequence<byte>(new byte[MezonTransportFrameCodec.MaxApiResponseLen - 10]);
        Assert.False(MezonTransportFrameCodec.AppendApiChunk(apiChunkBuffers, 3, first, finished: false, ref code, out _));

        var second = new ReadOnlySequence<byte>(new byte[100]);
        Assert.True(MezonTransportFrameCodec.AppendApiChunk(apiChunkBuffers, 3, second, finished: false, ref code, out var frame));

        Assert.Equal(MezonTransportFrameCodec.ApiResponseTooLargeCode, code);
        Assert.True(frame.IsEmpty);
        Assert.Empty(apiChunkBuffers);
    }

    // Vectors from mezon-desktop abridged_tcp_adapter.rs protobuf_message_len tests.
    [Theory]
    [InlineData(new byte[] { 0x10, 0x80, 0xa0, 0x80, 0xf0, 0xfe, 0xd1, 0xd3, 0xc5, 0x19, 0x0c, 0xc2, 0x01, 0x2a }, 10)]
    [InlineData(new byte[] { 0x10, 0x80, 0xa0, 0x80, 0xf0, 0xfe, 0xd1, 0xd3, 0xc5, 0x19 }, 10)]
    [InlineData(new byte[] { 0x0a, 0x03, 0x61, 0x62, 0x63, 0x17, 0x32, 0x57 }, 5)]
    [InlineData(new byte[] { 0x10, 0x80, 0xa0 }, -1)]
    [InlineData(new byte[] { 0x0a, 0x05, 0x61, 0x62 }, -1)]
    [InlineData(new byte[] { 0x0a, 0xff, 0xff, 0xff, 0xff, 0x0f, 0x00, 0x00 }, 0)]
    [InlineData(new byte[] { 0x08, 0x58, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00 }, 4)]
    public void ProtobufMessageLength_MatchesRustVectors(byte[] buffer, int expected)
    {
        Assert.Equal(expected, MezonTransportFrameCodec.ProtobufMessageLength(buffer));
    }

    [Fact]
    public void TrimPadding_KeepsEnvelopeEndingInEmptyField()
    {
        // Envelope{cid=88, channel_join={}} legitimately ends in 0x00 and has no padding.
        var frame = new ReadOnlyMemory<byte>([0x08, 0x58, 0x22, 0x00]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal(4, trimmed.Length);
        var envelope = Mezon.Net.Internal.Realtime.Envelope.Parser.ParseFrom(trimmed.Span);
        Assert.Equal(88, envelope.Cid);
        Assert.Equal(Mezon.Net.Internal.Realtime.Envelope.MessageOneofCase.ChannelJoin, envelope.MessageCase);
    }

    [Fact]
    public void TrimPadding_StripsZeroPadding()
    {
        var frame = new ReadOnlyMemory<byte>([0x08, 0x2A, 0x00, 0x00]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal([0x08, 0x2A], trimmed.ToArray());
    }

    [Fact]
    public void TrimPadding_KeepsFullFrameWhenFieldIsTruncated()
    {
        var frame = new ReadOnlyMemory<byte>([0x0A, 0x05, 0x61, 0x62]);
        var trimmed = MezonTransportFrameCodec.TrimRealtimePadding(frame);
        Assert.Equal(4, trimmed.Length);
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
        var bytes = MezonTransportFrameBuilder.BuildAbridgedFrame([0x0A, 0x01, 0x41]);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out _,
            out _,
            out var frame));
        Assert.Equal(MezonMessageType.Realtime, type);
        Assert.Equal([0x0A, 0x01, 0x41], frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_Abridged_KeepsTrailingZeroOfEmptyField()
    {
        var bytes = MezonTransportFrameBuilder.BuildAbridgedFrame([0x08, 0x58, 0x22, 0x00]);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out _,
            out _,
            out _,
            out var frame));
        Assert.Equal([0x08, 0x58, 0x22, 0x00], frame.ToArray());
    }

    [Fact]
    public void TryReadFrame_LargeAbridgedFrame_IsAccepted()
    {
        // 64 KiB realtime push: larger than the old 8 KiB cap, which tore down the connection.
        var payload = new byte[64 * 1024];
        payload[0] = 0x0A;
        payload[1] = 0xFC;
        payload[2] = 0xFF;
        payload[3] = 0x03; // field 1, length 65532
        payload.AsSpan(4).Fill(0x41);
        var bytes = MezonTransportFrameBuilder.BuildAbridgedFrame(payload);
        var buffer = new ReadOnlySequence<byte>(bytes);
        Assert.True(MezonTransportFrameCodec.TryReadFrame(
            ref buffer,
            new System.Collections.Concurrent.ConcurrentDictionary<int, ArrayBufferWriter<byte>>(),
            out var type,
            out _,
            out _,
            out var frame));
        Assert.Equal(MezonMessageType.Realtime, type);
        Assert.Equal(payload.Length, frame.Length);
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
        var header = new byte[] { 0x7f, 0x01, 0x00, 0x04 }; // lenDiv4 = 0x40001 → payload = 1 MiB + 4
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
