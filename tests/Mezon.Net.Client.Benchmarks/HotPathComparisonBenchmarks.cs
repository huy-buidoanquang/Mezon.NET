using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Mezon.Net.Transport.Internal;

namespace Mezon.Net.Client.Benchmarks
{
    /// <summary>
    /// Hot paths whose implementation changed in 1.7.0, written against APIs that exist in both 1.6.2 and 1.7.0 so the
    /// same file can be run on either tree for a before/after comparison.
    /// </summary>
    [MemoryDiagnoser]
    public class RealtimeEnvelopeDecodeBenchmarks
    {
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _apiChunks = new();
        private ReadOnlySequence<byte> _frame;

        /// <summary>Message text length; the envelope is framed with the server's 4-byte zero padding.</summary>
        [Params(16, 512, 3000)]
        public int ContentLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var envelope = new Envelope
            {
                Cid = 0,
                ChannelMessage = new Mezon.Net.Internal.Api.ChannelMessage
                {
                    ClanId = 1_840_000_000_000_000_001,
                    ChannelId = 1_840_000_000_000_000_002,
                    MessageId = 1_840_000_000_000_000_003,
                    SenderId = 1_840_000_000_000_000_004,
                    Username = "bench-user",
                    Content = "{\"t\":\"" + new string('x', ContentLength) + "\"}",
                },
            };
            var payload = envelope.ToByteArray();
            var padding = (4 - (payload.Length % 4)) & 3;
            var lengthInWords = (payload.Length + padding) / 4;
            var headerSize = lengthInWords < 127 ? 1 : 4;
            var frame = new byte[headerSize + payload.Length + padding];
            if (headerSize == 1)
            {
                frame[0] = (byte)lengthInWords;
            }
            else
            {
                frame[0] = MezonTransportFrameCodec.AbridgedExtendedPrefix;
                frame[1] = (byte)lengthInWords;
                frame[2] = (byte)(lengthInWords >> 8);
                frame[3] = (byte)(lengthInWords >> 16);
            }

            payload.CopyTo(frame.AsSpan(headerSize));
            _frame = new ReadOnlySequence<byte>(frame);
        }

        [Benchmark]
        public int DecodeFrame()
        {
            var buffer = _frame;
            MezonTransportFrameCodec.TryReadFrame(ref buffer, _apiChunks, out _, out _, out _, out var frame);
            return frame.Length;
        }

        [Benchmark]
        public long DecodeFrameAndParseEnvelope()
        {
            var buffer = _frame;
            MezonTransportFrameCodec.TryReadFrame(ref buffer, _apiChunks, out _, out _, out _, out var frame);
            return Envelope.Parser.ParseFrom(frame.Span).ChannelMessage.MessageId;
        }
    }

    [MemoryDiagnoser]
    public class CorrelationTimeoutBenchmarks
    {
        private SocketCorrelationHub _hub = null!;

        [GlobalSetup]
        public void Setup() => _hub = new SocketCorrelationHub();

        /// <summary>The real request path: the timeout is armed after the send, then the response arrives.</summary>
        [Benchmark]
        public async ValueTask<int> RegisterStartTimeoutCompleteAwait()
        {
            var cid = _hub.AllocateCid();
            var pending = _hub.Register(cid);
            pending.StartTimeout(10_000);
            _hub.TryComplete(cid, 0, ReadOnlyMemory<byte>.Empty);
            var response = await pending.Task;
            return response.Code;
        }
    }

    /// <summary>
    /// End-to-end engine dispatch of 1,000 channel messages spread over 8 channels, from the receive callback to the
    /// subscriber, with the default options of each version (1.6.2: Task.Yield per event; 1.7.0: ordered lanes).
    /// </summary>
    [MemoryDiagnoser]
    public class RealtimeDispatchBenchmarks
    {
        private const int EventCount = 1_000;
        private MezonClient _client = null!;
        private Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task> _receive = null!;
        private Envelope[] _envelopes = null!;
        private int _received;
        private TaskCompletionSource<bool> _done = null!;

        [GlobalSetup]
        public void Setup()
        {
            _client = new MezonClient(new MezonSocketClientOptions { LogLevel = Mezon.Net.Logging.LogLevel.Error });
            var method = typeof(MezonClient).GetMethod("SocketMessageHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _receive = (Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task>)method.CreateDelegate(
                typeof(Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task>), _client);
            _client.ChannelMessageReceivedEvent += OnMessage;

            _envelopes = new Envelope[EventCount];
            for (var i = 0; i < EventCount; i++)
            {
                _envelopes[i] = new Envelope
                {
                    ChannelMessage = new Mezon.Net.Internal.Api.ChannelMessage
                    {
                        ClanId = 1,
                        ChannelId = 100 + (i % 8),
                        MessageId = i + 1,
                        Content = "{\"t\":\"hello\"}",
                    },
                };
            }
        }

        private Task OnMessage(ChannelMessageEventData _)
        {
            if (Interlocked.Increment(ref _received) == EventCount)
            {
                _done.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        [Benchmark(OperationsPerInvoke = EventCount)]
        public async Task DispatchChannelMessages()
        {
            _received = 0;
            _done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            for (var i = 0; i < _envelopes.Length; i++)
            {
                await _receive(MezonMessageType.Realtime, 0, 0, default, _envelopes[i]);
            }

            await _done.Task;
        }
    }

    [MemoryDiagnoser]
    public class MessageContentLayoutBenchmarks
    {
        private const string RichRaw =
            "{\"t\":\"hello #general :wave:\",\"hg\":[{\"channelId\":\"5\",\"s\":6,\"e\":14}],\"ej\":[{\"emojiid\":\"7\",\"s\":15,\"e\":21}],\"embed\":[{\"title\":\"x\",\"description\":\"y\"}]}";

        /// <summary>Received message whose handler only reads the text (the common bot path).</summary>
        [Benchmark]
        public string? ParseAndReadText() => MessageContent.Parse(RichRaw).Text;

        /// <summary>Received message whose handler reads typed tokens, which materializes the snapshot.</summary>
        [Benchmark]
        public int ParseAndReadTypedTokens()
        {
            var content = MessageContent.Parse(RichRaw);
            return (content.Hashtags?.Count ?? 0) + (content.Emojis?.Count ?? 0) + (content.Embeds?.Count ?? 0);
        }

        /// <summary>Typed tokens read twice: the second read must reuse the cached snapshot.</summary>
        [Benchmark]
        public int ParseAndReadTypedTokensTwice()
        {
            var content = MessageContent.Parse(RichRaw);
            return (content.Hashtags?.Count ?? 0) + (content.Hashtags?.Count ?? 0);
        }
    }
}
