using BenchmarkDotNet.Attributes;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class EphemeralQuickMenuBenchmarks
    {
        private SendEphemeralMessageParams _update = default;
        private QuickMenuDataEvent _quickMenu = null!;

        [GlobalSetup]
        public void Setup()
        {
            _update = new SendEphemeralMessageParams(
                new[] { 7001L },
                new SendChannelMessageParams(
                    clanId: 11,
                    channelId: 22,
                    content: "{\"t\":\"updated\"}",
                    isPublic: false,
                    mode: 3,
                    code: 14,
                    id: 9001));
            _quickMenu = new QuickMenuDataEvent
            {
                MenuName = "ai-summary",
                SenderId = 10,
                MessageSenderId = 20,
                Message = new ChannelMessageSend
                {
                    ClanId = 30,
                    ChannelId = 40,
                    Id = 50,
                    Content = "{\"t\":\"summarize\"}",
                },
            };
        }

        [Benchmark]
        public Envelope BuildEphemeralUpdateEnvelope()
            => MessageSendHelper.ToEphemeralEnvelope(_update);

        [Benchmark]
        public QuickMenuReceivedEventData ProjectQuickMenuPayload()
            => new QuickMenuReceivedEventData(_quickMenu);
    }
}
