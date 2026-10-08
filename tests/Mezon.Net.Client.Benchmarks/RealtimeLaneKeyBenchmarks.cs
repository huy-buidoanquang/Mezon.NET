using BenchmarkDotNet.Attributes;
using Mezon.Net.Client.Dispatch;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Benchmarks
{
    /// <summary>Cost of picking the ordered-dispatch lane for an envelope (runs once per received event).</summary>
    [MemoryDiagnoser]
    public class RealtimeLaneKeyBenchmarks
    {
        private readonly Envelope _channelMessage = new()
        {
            ChannelMessage = new Mezon.Net.Internal.Api.ChannelMessage { ClanId = 1, ChannelId = 1_840_000_000_000_000_002 },
        };

        private readonly Envelope _nestedChannel = new()
        {
            UserChannelAddedEvent = new UserChannelAdded
            {
                ChannelDesc = new Mezon.Net.Internal.Api.ChannelDescription { ChannelId = 1_840_000_000_000_000_002 },
                ClanId = 1,
            },
        };

        [Benchmark]
        public long DirectChannelKey() => RealtimeLaneKey.Compute(_channelMessage);

        [Benchmark]
        public long NestedChannelKey() => RealtimeLaneKey.Compute(_nestedChannel);
    }
}
