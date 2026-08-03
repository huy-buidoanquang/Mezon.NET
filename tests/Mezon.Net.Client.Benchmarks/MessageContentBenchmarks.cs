using BenchmarkDotNet.Attributes;
using Mezon.Net.Client;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class MessageContentBenchmarks
    {
        private readonly string _raw = MessageContent.CreateText("hello world").RawJson;
        private readonly string _richRaw =
            "{\"t\":\"hello world\",\"embed\":[{\"title\":\"x\",\"description\":\"y\"}],\"question\":\"q\",\"answers\":[\"a\",\"b\"]}";
        private readonly MessageContent _parsed = MessageContent.CreateText("hello world");
        private readonly MessageContent _lazyParsed = MessageContent.Parse(
            "{\"t\":\"hello world\",\"embed\":[{\"title\":\"x\"}],\"custom\":true}");

        [Benchmark]
        public MessageContent ParseText() => MessageContent.Parse(_raw);

        [Benchmark]
        public MessageContent ParseRich() => MessageContent.Parse(_richRaw);

        [Benchmark]
        public string? AccessTextCreate() => _parsed.Text;

        [Benchmark]
        public string? AccessTextFastPath() => _lazyParsed.Text;

        [Benchmark]
        public string ToJson() => _parsed.ToJson();

        [Benchmark]
        public string ToJsonPassthrough() => _lazyParsed.ToJson();
    }
}
