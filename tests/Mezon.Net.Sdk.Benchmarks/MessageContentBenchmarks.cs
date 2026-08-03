using BenchmarkDotNet.Attributes;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Benchmarks
{
    [MemoryDiagnoser]
    public class MessageContentBenchmarks
    {
        private readonly string _raw = MessageContent.CreateText("hello world").RawJson;
        private readonly MessageContent _parsed = MessageContent.CreateText("hello world");
        private readonly MessageContent _lazyParsed = MessageContent.Parse(
            "{\"t\":\"hello world\",\"embed\":[{\"title\":\"x\"}],\"custom\":true}");

        [Benchmark]
        public MessageContent ParseText() => MessageContent.Parse(_raw);

        [Benchmark]
        public string ToJson() => _parsed.ToJson();

        [Benchmark]
        public string? AccessText() => _parsed.Text;

        [Benchmark]
        public string? AccessTextFastPath() => _lazyParsed.Text;
    }
}
