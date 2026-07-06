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
    }
}
