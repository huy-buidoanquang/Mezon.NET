using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class ProtoListViewBenchmarks
    {
        private ChannelMessageResponse _message;

        [GlobalSetup]
        public void Setup()
        {
            var list = new MessageMentionList();
            for (var i = 0; i < 8; i++)
            {
                list.Mentions.Add(new MessageMention { UserId = i + 1, Username = "u" + i });
            }

            var proto = new ChannelMessage
            {
                MessageId = 1,
                ChannelId = 2,
                Content = "{\"t\":\"hi\"}",
                Mentions = list.ToByteString(),
            };
            _message = ChannelMessageResponse.Decode(proto);
        }

        [Benchmark]
        public int AccessMentionsTenTimes()
        {
            var sum = 0;
            for (var i = 0; i < 10; i++)
            {
                sum += _message.Mentions.Count;
            }

            return sum;
        }
    }
}
