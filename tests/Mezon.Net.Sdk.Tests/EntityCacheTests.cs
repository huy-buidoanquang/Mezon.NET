using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Caching;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class EntityCacheTests
    {
        [Fact]
        public async Task GetOrFetchAsync_returns_cached_instance_without_factory_call()
        {
            var cache = new EntityCache<string>();
            cache.Set(1, "cached");
            var factoryCalls = 0;
            var value = await cache.GetOrFetchAsync(1, (_, __) =>
            {
                factoryCalls++;
                return new ValueTask<string>("new");
            });
            Assert.Equal("cached", value);
            Assert.Equal(0, factoryCalls);
        }

        [Fact]
        public async Task GetOrFetchAsync_single_flight_calls_factory_once()
        {
            var cache = new EntityCache<string>(capacity: 8);
            var calls = 0;
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async ValueTask<string> Factory(long _, CancellationToken __)
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult(true);
                await Task.Delay(50).ConfigureAwait(false);
                return "created";
            }

            var t1 = cache.GetOrFetchAsync(7, Factory).AsTask();
            await started.Task.ConfigureAwait(false);
            var t2 = cache.GetOrFetchAsync(7, Factory).AsTask();
            var results = await Task.WhenAll(t1, t2).ConfigureAwait(false);

            Assert.Equal("created", results[0]);
            Assert.Equal("created", results[1]);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Set_evicts_lru_when_over_capacity()
        {
            var cache = new EntityCache<string>(capacity: 2);
            cache.Set(1, "a");
            cache.Set(2, "b");
            _ = cache.Get(1);
            cache.Set(3, "c");

            Assert.Equal("a", cache.Get(1));
            Assert.Null(cache.Get(2));
            Assert.Equal("c", cache.Get(3));
            Assert.Equal(2, cache.Count);
        }
    }
}
