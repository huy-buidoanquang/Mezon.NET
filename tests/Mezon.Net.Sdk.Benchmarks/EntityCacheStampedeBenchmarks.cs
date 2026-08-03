using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Mezon.Net.Sdk.Caching;

namespace Mezon.Net.Sdk.Benchmarks
{
    [MemoryDiagnoser]
    public class EntityCacheStampedeBenchmarks
    {
        private EntityCache<string> _cache = null!;
        private int _factoryCalls;

        [GlobalSetup]
        public void Setup()
        {
            _cache = new EntityCache<string>(capacity: 128);
            _factoryCalls = 0;
        }

        [Benchmark]
        public async Task<int> ConcurrentMissSingleFlight()
        {
            _cache.Clear();
            Volatile.Write(ref _factoryCalls, 0);

            async ValueTask<string> Factory(long id, CancellationToken ct)
            {
                Interlocked.Increment(ref _factoryCalls);
                await Task.Yield();
                return "v" + id;
            }

            var tasks = new Task<string>[32];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = _cache.GetOrFetchAsync(99, Factory).AsTask();
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return Volatile.Read(ref _factoryCalls);
        }
    }
}
