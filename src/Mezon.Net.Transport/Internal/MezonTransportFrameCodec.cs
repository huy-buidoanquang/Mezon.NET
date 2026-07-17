using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Mezon.Net.Core;

namespace Mezon.Net.Transport.Internal
{
    internal static class MezonTransportFrameCodec
    {
        public const byte PingPongPrefix = 0x00;
        public const byte ApiPrefix = 0xff;
        public const byte AbridgedExtendedPrefix = 0x7f;
        public const int FinishFlag = 0xff;

        public static bool TryReadFrame(
            ref ReadOnlySequence<byte> buffer,
            ConcurrentDictionary<int, ArrayBufferWriter<byte>> apiChunkBuffers,
            out MezonMessageType type,
            out int cid,
            out int code,
            out ReadOnlyMemory<byte> frame)
        {
            type = MezonMessageType.Heartbeat;
            cid = -1;
            code = -1;
            frame = ReadOnlyMemory<byte>.Empty;
            if (buffer.IsEmpty)
            {
                return false;
            }

            var reader = new SequenceReader<byte>(buffer);
            if (!reader.TryRead(out byte prefix))
            {
                return false;
            }

            switch (prefix)
            {
                case PingPongPrefix:
                    if (TryReadPongFrame(ref reader, out cid))
                    {
                        code = 0;
                        type = MezonMessageType.Heartbeat;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }

                    return false;
                case ApiPrefix:
                {
                    var consumedBefore = reader.Consumed;
                    if (TryReadApiFrame(ref reader, apiChunkBuffers, out cid, out code, out frame))
                    {
                        type = MezonMessageType.Api;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }

                    if (reader.Consumed > consumedBefore)
                    {
                        buffer = buffer.Slice(reader.Position);
                    }

                    return false;
                }
                default:
                    if (TryReadRealtimeFrame(ref reader, prefix, out frame))
                    {
                        code = 0;
                        type = MezonMessageType.Realtime;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }

                    return false;
            }
        }

        public static ReadOnlyMemory<byte> TrimRealtimePadding(ReadOnlyMemory<byte> frame)
        {
            var span = frame.Span;
            int len = span.Length;
            int maxPadding = len < 3 ? len : 3;
            int trimmed = 0;
            while (trimmed < maxPadding && span[len - 1 - trimmed] == 0x00)
            {
                trimmed++;
            }

            return trimmed == 0 ? frame : frame.Slice(0, len - trimmed);
        }

        public static bool TryQueueRealtimeFrame(ChannelWriter<ReadOnlyMemory<byte>> writer, ReadOnlyMemory<byte> data)
        {
            int padding = (4 - (data.Length % 4)) & 3;
            int payloadWithPadding = data.Length + padding;
            int lenDiv4 = payloadWithPadding / 4;
            int headerSize = lenDiv4 < 127 ? 1 : 4;
            int totalSize = headerSize + payloadWithPadding;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            Span<byte> span = buffer.AsSpan(0, totalSize);
            if (headerSize == 1)
            {
                span[0] = (byte)lenDiv4;
            }
            else
            {
                span[0] = AbridgedExtendedPrefix;
                span[1] = (byte)lenDiv4;
                span[2] = (byte)(lenDiv4 >> 8);
                span[3] = (byte)(lenDiv4 >> 16);
            }

            data.Span.CopyTo(span.Slice(headerSize));
            if (padding > 0)
            {
                span.Slice(headerSize + data.Length, padding).Clear();
            }

            if (!writer.TryWrite(buffer.AsMemory(0, totalSize)))
            {
                ArrayPool<byte>.Shared.Return(buffer);
                return false;
            }

            return true;
        }

        public static bool TryQueuePingFrame(ChannelWriter<ReadOnlyMemory<byte>> writer, ushort cid)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(3);
            Span<byte> span = buffer.AsSpan(0, 3);
            span[0] = PingPongPrefix;
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1), cid);
            if (!writer.TryWrite(buffer.AsMemory(0, 3)))
            {
                ArrayPool<byte>.Shared.Return(buffer);
                return false;
            }

            return true;
        }

        public static void ReturnPooledSendBuffer(ReadOnlyMemory<byte> data)
        {
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null && segment.Count > 0)
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }

        private static bool TryReadPongFrame(ref SequenceReader<byte> reader, out int cid)
        {
            cid = -1;
            if (reader.Remaining < 2 || !reader.TryReadBigEndian(out short cidS))
            {
                return false;
            }

            cid = cidS;
            return true;
        }

        private static bool TryReadApiFrame(
            ref SequenceReader<byte> reader,
            ConcurrentDictionary<int, ArrayBufferWriter<byte>> apiChunkBuffers,
            out int cid,
            out int code,
            out ReadOnlyMemory<byte> frame)
        {
            cid = -1;
            code = -1;
            frame = ReadOnlyMemory<byte>.Empty;
            const int headerSize = 10; // cid(2) + code(4) + payloadLen(4)
            if (reader.Remaining < headerSize)
            {
                return false;
            }

            // Peek payload length before consuming — incomplete frames must not advance the reader.
            var peek = reader;
            peek.TryReadBigEndian(out short _);
            peek.TryReadBigEndian(out int _);
            peek.TryReadBigEndian(out int payloadLen);
            if (reader.Remaining < headerSize + payloadLen)
            {
                return false;
            }

            reader.TryReadBigEndian(out short cidFrame);
            cid = cidFrame;
            reader.TryReadBigEndian(out int codeFrame);
            reader.TryReadBigEndian(out payloadLen);

            var writer = apiChunkBuffers.GetOrAdd(cid, _ => new ArrayBufferWriter<byte>(initialCapacity: 4096));
            var span = writer.GetSpan(payloadLen);
            var payloadSlice = reader.Sequence.Slice(reader.Position, payloadLen);
            if (payloadSlice.Length < payloadLen)
            {
                return false;
            }

            payloadSlice.CopyTo(span);
            writer.Advance(payloadLen);
            code = (codeFrame >> 16) & 0xffff;
            var finishFlag = codeFrame & 0xffff;
            reader.Advance(payloadLen);
            if (finishFlag == FinishFlag)
            {
                frame = writer.WrittenMemory;
                apiChunkBuffers.TryRemove(cid, out _);
                return true;
            }

            return false;
        }

        private static bool TryReadRealtimeFrame(ref SequenceReader<byte> reader, byte prefix, out ReadOnlyMemory<byte> frame)
        {
            frame = ReadOnlyMemory<byte>.Empty;
            int payloadLen;
            if (prefix < AbridgedExtendedPrefix)
            {
                payloadLen = prefix * 4;
            }
            else if (prefix == AbridgedExtendedPrefix)
            {
                if (reader.Remaining < 3)
                {
                    return false;
                }

                reader.TryRead(out byte l1);
                reader.TryRead(out byte l2);
                reader.TryRead(out byte l3);
                payloadLen = (l1 | (l2 << 8) | (l3 << 16)) * 4;
            }
            else
            {
                return false;
            }

            if (reader.Remaining < payloadLen)
            {
                return false;
            }

            var rawPayloadSequence = reader.Sequence.Slice(reader.Position, payloadLen);
            frame = rawPayloadSequence.IsSingleSegment
                ? rawPayloadSequence.First.Slice(0, payloadLen)
                : rawPayloadSequence.ToArray();
            reader.Advance(payloadLen);
            return true;
        }
    }
}
