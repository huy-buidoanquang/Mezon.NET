using System;
using System.Linq;
using System.Threading.Tasks;
using Mezon.Net.SDK;

namespace Mezon.Net.SDK.Tests
{
    /// <summary>
    /// Unit tests for CacheManager and Collection
    /// Note: This is a template. You'll need to add a test framework like xUnit, NUnit, or MSTest
    /// </summary>
    public class CacheManagerTests
    {
        // [Fact] // Uncomment when using xUnit
        public async Task FetchAsync_ShouldCallFetcherOnce_WhenKeyNotInCache()
        {
            // Arrange
            int fetchCount = 0;
            var cache = new CacheManager<string, string>(
                fetcher: async (key) =>
                {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Value_{key}";
                },
                maxSize: 10
            );

            // Act
            var result = await cache.FetchAsync("key1");

            // Assert
            // Assert.Equal(1, fetchCount);
            // Assert.Equal("Value_key1", result);
        }

        // [Fact]
        public async Task FetchAsync_ShouldReturnCachedValue_WhenKeyExists()
        {
            // Arrange
            int fetchCount = 0;
            var cache = new CacheManager<string, string>(
                fetcher: async (key) =>
                {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Value_{key}";
                },
                maxSize: 10
            );

            // Act
            await cache.FetchAsync("key1");
            var result = await cache.FetchAsync("key1");

            // Assert
            // Assert.Equal(1, fetchCount); // Fetcher called only once
            // Assert.Equal("Value_key1", result);
        }

        // [Fact]
        public async Task Set_ShouldEvictOldestEntry_WhenCacheFull()
        {
            // Arrange
            var cache = new CacheManager<int, string>(
                fetcher: async (key) => await Task.FromResult($"Value_{key}"),
                maxSize: 3
            );

            // Act
            cache.Set(1, "Value_1");
            cache.Set(2, "Value_2");
            cache.Set(3, "Value_3");
            cache.Set(4, "Value_4"); // Should evict key 1

            // Assert
            // Assert.False(cache.Has(1));
            // Assert.True(cache.Has(2));
            // Assert.True(cache.Has(3));
            // Assert.True(cache.Has(4));
        }

        // [Fact]
        public void Filter_ShouldReturnMatchingEntries()
        {
            // Arrange
            var cache = new CacheManager<int, TestItem>(
                fetcher: async (key) => await Task.FromResult(new TestItem { Id = key, IsActive = key % 2 == 0 }),
                maxSize: 10
            );

            cache.Set(1, new TestItem { Id = 1, IsActive = false });
            cache.Set(2, new TestItem { Id = 2, IsActive = true });
            cache.Set(3, new TestItem { Id = 3, IsActive = false });
            cache.Set(4, new TestItem { Id = 4, IsActive = true });

            // Act
            var activeItems = cache.Filter(item => item.IsActive);

            // Assert
            // Assert.Equal(2, activeItems.Count);
        }

        // [Fact]
        public void Map_ShouldTransformAllValues()
        {
            // Arrange
            var cache = new CacheManager<int, TestItem>(
                fetcher: async (key) => await Task.FromResult(new TestItem { Id = key }),
                maxSize: 10
            );

            cache.Set(1, new TestItem { Id = 1, Name = "Item1" });
            cache.Set(2, new TestItem { Id = 2, Name = "Item2" });
            cache.Set(3, new TestItem { Id = 3, Name = "Item3" });

            // Act
            var names = cache.Map(item => item.Name);

            // Assert
            // Assert.Equal(3, names.Length);
            // Assert.Contains("Item1", names);
        }

        // [Fact]
        public void Find_ShouldReturnFirstMatch()
        {
            // Arrange
            var cache = new CacheManager<int, TestItem>(
                fetcher: async (key) => await Task.FromResult(new TestItem { Id = key }),
                maxSize: 10
            );

            cache.Set(1, new TestItem { Id = 1, Name = "Item1" });
            cache.Set(2, new TestItem { Id = 2, Name = "Target" });
            cache.Set(3, new TestItem { Id = 3, Name = "Item3" });

            // Act
            var found = cache.Find(item => item.Name == "Target");

            // Assert
            // Assert.NotNull(found);
            // Assert.Equal(2, found.Id);
        }

        // [Fact]
        public async Task ConcurrentFetch_ShouldCallFetcherOnlyOnce()
        {
            // Arrange
            int fetchCount = 0;
            var cache = new CacheManager<string, string>(
                fetcher: async (key) =>
                {
                    System.Threading.Interlocked.Increment(ref fetchCount);
                    await Task.Delay(100); // Simulate slow API
                    return $"Value_{key}";
                },
                maxSize: 10
            );

            // Act
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => cache.FetchAsync("same_key"))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            // Assert.Equal(1, fetchCount); // Should only fetch once despite 10 concurrent calls
            // Assert.All(results, r => Assert.Equal("Value_same_key", r));
        }

        // [Fact]
        public void Collection_Sort_ShouldOrderElements()
        {
            // Arrange
            var collection = new Collection<string, TestItem>();
            collection.Set("a", new TestItem { Id = 3, Name = "C" });
            collection.Set("b", new TestItem { Id = 1, Name = "A" });
            collection.Set("c", new TestItem { Id = 2, Name = "B" });

            // Act
            var sorted = collection.Sort((a, b) => a.Id.CompareTo(b.Id));

            // Assert
            var ids = sorted.Map(item => item.Id);
            // Assert.Equal(new[] { 1, 2, 3 }, ids);
        }

        // [Fact]
        public void Collection_Reduce_ShouldAccumulateValues()
        {
            // Arrange
            var collection = new Collection<string, TestItem>();
            collection.Set("a", new TestItem { Id = 1, Value = 10 });
            collection.Set("b", new TestItem { Id = 2, Value = 20 });
            collection.Set("c", new TestItem { Id = 3, Value = 30 });

            // Act
            var sum = collection.Reduce(0, (acc, item) => acc + item.Value);

            // Assert
            // Assert.Equal(60, sum);
        }

        // [Fact]
        public void Collection_Some_ShouldDetectMatch()
        {
            // Arrange
            var collection = new Collection<string, TestItem>();
            collection.Set("a", new TestItem { IsActive = false });
            collection.Set("b", new TestItem { IsActive = true });
            collection.Set("c", new TestItem { IsActive = false });

            // Act
            var hasActive = collection.Some(item => item.IsActive);
            var hasInactive = collection.Some(item => !item.IsActive);

            // Assert
            // Assert.True(hasActive);
            // Assert.True(hasInactive);
        }

        // [Fact]
        public void Collection_Every_ShouldCheckAllElements()
        {
            // Arrange
            var collection = new Collection<string, TestItem>();
            collection.Set("a", new TestItem { IsActive = true });
            collection.Set("b", new TestItem { IsActive = true });
            collection.Set("c", new TestItem { IsActive = true });

            // Act
            var allActive = collection.Every(item => item.IsActive);

            // Assert
            // Assert.True(allActive);
        }

        // [Fact]
        public void Delete_ShouldRemoveEntry_AndReturnTrue()
        {
            // Arrange
            var cache = new CacheManager<string, string>(
                fetcher: async (key) => await Task.FromResult($"Value_{key}"),
                maxSize: 10
            );

            cache.Set("key1", "value1");

            // Act
            var deleted = cache.Delete("key1");
            var stillExists = cache.Has("key1");

            // Assert
            // Assert.True(deleted);
            // Assert.False(stillExists);
        }

        // [Fact]
        public void Clear_ShouldRemoveAllEntries()
        {
            // Arrange
            var cache = new CacheManager<string, string>(
                fetcher: async (key) => await Task.FromResult($"Value_{key}"),
                maxSize: 10
            );

            cache.Set("key1", "value1");
            cache.Set("key2", "value2");
            cache.Set("key3", "value3");

            // Act
            cache.Clear();

            // Assert
            // Assert.Equal(0, cache.Count);
            // Assert.False(cache.Has("key1"));
        }

        #region Helper Classes

        private class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
            public int Value { get; set; }
        }

        #endregion
    }
}
