using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Mezon.Net.Sdk.Caching;

namespace Mezon.Net.Sdk.Benchmarks
{
    [MemoryDiagnoser]
    public class EntityCacheBenchmarks
    {
        private readonly EntityCache<string> _cache = new EntityCache<string>(512);

        [GlobalSetup]
        public void Setup() => _cache.Set(42, "value");

        [Benchmark]
        public async ValueTask<string> CacheHit()
        {
            return await _cache.GetOrFetchAsync(42, static (_, __) => new ValueTask<string>("miss"));
        }
    }
}
