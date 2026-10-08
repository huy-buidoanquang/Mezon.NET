using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Mezon.Net.Core;

namespace Mezon.Net.Transport.Internal
{
    internal static class MezonWebSocketFrameCodec
    {
        public const int ApiHeaderLength = 7;

        public static bool TryQueueRawFrame(ChannelWriter<ReadOnlyMemory<byte>> writer, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0)
            {
                return false;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(data.Length);
            data.Span.CopyTo(buffer);
            if (!writer.TryWrite(buffer.AsMemory(0, data.Length)))
            {
                ArrayPool<byte>.Shared.Return(buffer);
                return false;
            }

            return true;
        }

        public static bool TryHandleMessage(
            ReadOnlyMemory<byte> message,
            ConcurrentDictionary<int, ArrayBufferWriter<byte>> apiChunkBuffers,
            out MezonMessageType type,
            out int cid,
            out int code,
            out ReadOnlyMemory<byte> payload)
        {
            type = default;
            cid = default;
            code = default;
            payload = default;
            if (message.Length == 0)
            {
                return false;
            }

            var span = message.Span;
            if (span[0] == MezonTransportFrameCodec.ApiPrefix)
            {
                if (span.Length < ApiHeaderLength)
                {
                    return false;
                }

                cid = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(1, 2));
                var codeField = BinaryPrimitives.ReadInt32BigEndian(span.Slice(3, 4));
                code = (codeField >> 16) & 0xffff;
                var finished = (codeField & 0xffff) == MezonTransportFrameCodec.FinishFlag;
                var chunk = new ReadOnlySequence<byte>(message.Slice(ApiHeaderLength));
                if (!MezonTransportFrameCodec.AppendApiChunk(apiChunkBuffers, cid, chunk, finished, ref code, out payload))
                {
                    return false;
                }

                type = MezonMessageType.Api;
                return true;
            }

            type = MezonMessageType.Realtime;
            payload = message;
            return true;
        }

        public static void ReturnPooledSendBuffer(ReadOnlyMemory<byte> data)
        {
            if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array != null && segment.Count > 0)
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }
    }
}
