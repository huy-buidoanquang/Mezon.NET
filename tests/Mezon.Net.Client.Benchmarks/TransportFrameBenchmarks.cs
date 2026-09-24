using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Mezon.Net.Core;
using Mezon.Net.Transport.Internal;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class TransportFrameBenchmarks
    {
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _apiChunks = new();
        private Channel<ReadOnlyMemory<byte>> _sendChannel = null!;
        private byte[] _payload = null!;
        private byte[] _realtimeFrame = null!;
        private ReadOnlySequence<byte> _fragmentedRealtimeFrame;
        private byte[] _apiFrame = null!;

        [Params(64, 256, 1024, 4092)]
        public int PayloadSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
            _payload = new byte[PayloadSize];
            Array.Fill(_payload, (byte)0x2a);

            var padding = (4 - (PayloadSize % 4)) & 3;
            var payloadWithPadding = PayloadSize + padding;
            var lengthInWords = payloadWithPadding / 4;
            var headerSize = lengthInWords < 127 ? 1 : 4;
            _realtimeFrame = new byte[headerSize + payloadWithPadding];
            if (headerSize == 1)
            {
                _realtimeFrame[0] = (byte)lengthInWords;
            }
            else
            {
                _realtimeFrame[0] = MezonTransportFrameCodec.AbridgedExtendedPrefix;
                _realtimeFrame[1] = (byte)lengthInWords;
                _realtimeFrame[2] = (byte)(lengthInWords >> 8);
                _realtimeFrame[3] = (byte)(lengthInWords >> 16);
            }

            _payload.CopyTo(_realtimeFrame.AsSpan(headerSize));
            var split = Math.Max(1, _realtimeFrame.Length / 2);
            var first = new SequenceSegment(_realtimeFrame.AsMemory(0, split));
            var second = first.Append(_realtimeFrame.AsMemory(split));
            _fragmentedRealtimeFrame = new ReadOnlySequence<byte>(first, 0, second, second.Memory.Length);

            _apiFrame = new byte[11 + PayloadSize];
            _apiFrame[0] = MezonTransportFrameCodec.ApiPrefix;
            BinaryPrimitives.WriteUInt16BigEndian(_apiFrame.AsSpan(1, 2), 42);
            BinaryPrimitives.WriteInt32BigEndian(_apiFrame.AsSpan(3, 4), MezonTransportFrameCodec.FinishFlag);
            BinaryPrimitives.WriteInt32BigEndian(_apiFrame.AsSpan(7, 4), PayloadSize);
            _payload.CopyTo(_apiFrame.AsSpan(11));
        }

        [Benchmark]
        public int QueueAndDrainRealtimeFrame()
        {
            if (!MezonTransportFrameCodec.TryQueueRealtimeFrame(_sendChannel.Writer, _payload)
                || !_sendChannel.Reader.TryRead(out var frame))
            {
                return 0;
            }

            var length = frame.Length;
            MezonTransportFrameCodec.ReturnPooledSendBuffer(frame);
            return length;
        }

        [Benchmark]
        public int DecodeSingleSegmentRealtimeFrame()
        {
            var sequence = new ReadOnlySequence<byte>(_realtimeFrame);
            return MezonTransportFrameCodec.TryReadFrame(
                ref sequence,
                _apiChunks,
                out _,
                out _,
                out _,
                out var frame)
                ? frame.Length
                : 0;
        }

        [Benchmark]
        public int DecodeFragmentedRealtimeFrame()
        {
            var sequence = _fragmentedRealtimeFrame;
            return MezonTransportFrameCodec.TryReadFrame(
                ref sequence,
                _apiChunks,
                out _,
                out _,
                out _,
                out var frame)
                ? frame.Length
                : 0;
        }

        [Benchmark]
        public int DecodeApiResponseFrame()
        {
            var sequence = new ReadOnlySequence<byte>(_apiFrame);
            return MezonTransportFrameCodec.TryReadFrame(
                ref sequence,
                _apiChunks,
                out _,
                out _,
                out _,
                out var frame)
                ? frame.Length
                : 0;
        }

        private sealed class SequenceSegment : ReadOnlySequenceSegment<byte>
        {
            public SequenceSegment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public SequenceSegment Append(ReadOnlyMemory<byte> memory)
            {
                var segment = new SequenceSegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = segment;
                return segment;
            }
        }
    }
}
