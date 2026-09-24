using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Mezon.Net.Client.Benchmarks
{
    [MemoryDiagnoser]
    public class SocketCorrelationBenchmarks
    {
        private SocketCorrelationHub _hub = null!;
        private CancellationTokenSource _cancellation = null!;

        [GlobalSetup]
        public void Setup()
        {
            _hub = new SocketCorrelationHub();
            _cancellation = new CancellationTokenSource();
        }

        [GlobalCleanup]
        public void Cleanup() => _cancellation.Dispose();

        [Benchmark(Baseline = true)]
        public async ValueTask<int> RegisterCompleteAwait()
        {
            var cid = _hub.AllocateCid();
            var pending = _hub.Register(cid);
            _hub.TryComplete(cid, 0, ReadOnlyMemory<byte>.Empty);
            var response = await pending.Task;
            return response.Code;
        }

        [Benchmark]
        public async ValueTask<int> RegisterCancelableCompleteAwait()
        {
            var cid = _hub.AllocateCid();
            var pending = _hub.Register(cid, _cancellation.Token);
            _hub.TryComplete(cid, 0, ReadOnlyMemory<byte>.Empty);
            var response = await pending.Task;
            return response.Code;
        }
    }
}
