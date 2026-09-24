using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class ProtobufEnvelopeBenchmarks
    {
        private Envelope _envelope = null!;

        [GlobalSetup]
        public void Setup()
        {
            _envelope = new Envelope
            {
                Cid = 42,
                ChannelMessage = new ChannelMessage
                {
                    MessageId = 100,
                    ChannelId = 200,
                    ClanId = 300,
                    SenderId = 400,
                    Content = new string('x', 256),
                    Username = "benchmark-user"
                }
            };
        }

        [Benchmark(Baseline = true)]
        public byte[] CurrentPooledThenCopied()
        {
            var size = _envelope.CalculateSize();
            var rented = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                _envelope.WriteTo(rented.AsSpan(0, size));
                var payload = new byte[size];
                Buffer.BlockCopy(rented, 0, payload, 0, size);
                return payload;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        [Benchmark]
        public byte[] DirectToOwnedArray()
        {
            var size = _envelope.CalculateSize();
            var payload = new byte[size];
            _envelope.WriteTo(payload.AsSpan());
            return payload;
        }
    }
}
