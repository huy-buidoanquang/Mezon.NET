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
            ConcurrentDictionary<int, ArrayBufferWriter<byte>> apiStreams,
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
                var responseCode = (codeField >> 16) & 0xffff;
                var finishFlag = codeField & 0xffff;
                var chunk = span.Slice(ApiHeaderLength);

                var writer = apiStreams.GetOrAdd(cid, _ => new ArrayBufferWriter<byte>(initialCapacity: 4096));
                if (chunk.Length > 0)
                {
                    var target = writer.GetSpan(chunk.Length);
                    chunk.CopyTo(target);
                    writer.Advance(chunk.Length);
                }

                if (finishFlag != MezonTransportFrameCodec.FinishFlag)
                {
                    return false;
                }

                type = MezonMessageType.Api;
                code = responseCode;
                payload = writer.WrittenMemory;
                apiStreams.TryRemove(cid, out _);
                return true;
            }

            type = MezonMessageType.Abridged;
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
