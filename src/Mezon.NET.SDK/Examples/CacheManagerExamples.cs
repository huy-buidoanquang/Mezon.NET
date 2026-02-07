using System;
using System.Linq;
using System.Threading.Tasks;
using Mezon.NET.SDK.Abstractions;

namespace Mezon.NET.SDK.Examples
{
    /// <summary>
    /// Examples demonstrating the usage of CacheManager and Collection
    /// </summary>
    public static class CacheManagerExamples
    {
        /// <summary>
        /// Basic cache usage example
        /// </summary>
        public static async Task BasicUsageExample()
        {
            // Create a cache manager with a fetcher function
            var userCache = new CacheManager<string, User>(
                fetcher: async (userId) =>
                {
                    // Simulate API call
                    await Task.Delay(100);
                    return new User { Id = userId, Name = $"User_{userId}" };
                },
                maxSize: 100
            );

            // Get from cache (will fetch if not present)
            var user1 = await userCache.FetchAsync("user_1");
            Console.WriteLine($"Fetched: {user1.Name}");

            // Get again (will return from cache)
            var user1Cached = await userCache.FetchAsync("user_1");
            Console.WriteLine($"From cache: {user1Cached.Name}");

            // Manual set
            userCache.Set("user_2", new User { Id = "user_2", Name = "Manual User" });

            // Get without fetching
            var user2 = userCache.Get("user_2");
            Console.WriteLine($"Manual: {user2?.Name}");

            Console.WriteLine($"Cache size: {userCache.Count}");
        }

        /// <summary>
        /// Advanced filtering and mapping example
        /// </summary>
        public static async Task FilteringAndMappingExample()
        {
            var messageCache = new CacheManager<string, Message>(
                fetcher: async (msgId) =>
                {
                    await Task.Delay(50);
                    return new Message
                    {
                        Id = msgId,
                        Content = $"Message content {msgId}",
                        IsRead = false
                    };
                },
                maxSize: 50
            );

            // Add some messages
            for (int i = 1; i <= 10; i++)
            {
                await messageCache.FetchAsync($"msg_{i}");
                if (i % 2 == 0)
                {
                    var msg = messageCache.Get($"msg_{i}");
                    msg.IsRead = true;
                    messageCache.Set($"msg_{i}", msg);
                }
            }

            // Filter unread messages
            var unreadMessages = messageCache.Filter(msg => !msg.IsRead);
            Console.WriteLine($"Unread messages: {unreadMessages.Count}");

            // Map to message IDs
            var messageIds = messageCache.Map(msg => msg.Id);
            Console.WriteLine($"Message IDs: {string.Join(", ", messageIds)}");

            // Find specific message
            var foundMessage = messageCache.Find(msg => msg.Id == "msg_5");
            Console.WriteLine($"Found: {foundMessage?.Content}");

            // Check conditions
            bool hasUnread = messageCache.Some(msg => !msg.IsRead);
            bool allRead = messageCache.Every(msg => msg.IsRead);
            Console.WriteLine($"Has unread: {hasUnread}, All read: {allRead}");
        }

        /// <summary>
        /// Cache eviction example (FIFO when max size reached)
        /// </summary>
        public static async Task CacheEvictionExample()
        {
            var limitedCache = new CacheManager<int, string>(
                fetcher: async (id) =>
                {
                    await Task.Delay(10);
                    return $"Value_{id}";
                },
                maxSize: 5 // Small cache
            );

            // Fill cache
            for (int i = 1; i <= 5; i++)
            {
                await limitedCache.FetchAsync(i);
            }

            Console.WriteLine($"Cache filled: {limitedCache.Count} items");
            Console.WriteLine($"First key: {limitedCache.Cache.FirstKey()}");

            // Add one more - should evict first
            await limitedCache.FetchAsync(6);

            Console.WriteLine($"After adding 6th item: {limitedCache.Count} items");
            Console.WriteLine($"Has key 1: {limitedCache.Has(1)}"); // Should be false
            Console.WriteLine($"Has key 6: {limitedCache.Has(6)}"); // Should be true
        }

        /// <summary>
        /// Collection-specific operations example
        /// </summary>
        public static void CollectionOperationsExample()
        {
            var collection = new Collection<string, Product>();

            // Add items
            collection
                .Set("prod_1", new Product { Id = "prod_1", Name = "Laptop", Price = 1200 })
                .Set("prod_2", new Product { Id = "prod_2", Name = "Mouse", Price = 25 })
                .Set("prod_3", new Product { Id = "prod_3", Name = "Keyboard", Price = 75 });

            // Get first and last
            var first = collection.First();
            var last = collection.Last();
            Console.WriteLine($"First: {first?.Name}, Last: {last?.Name}");

            // Reduce example - calculate total price
            var totalPrice = collection.Reduce(0m, (acc, product) => acc + product.Price);
            Console.WriteLine($"Total price: ${totalPrice}");

            // Sort by price
            var sortedByPrice = collection.Sort((a, b) => a.Price.CompareTo(b.Price));
            Console.WriteLine("Products sorted by price:");
            foreach (var kvp in sortedByPrice)
            {
                Console.WriteLine($"  {kvp.Value.Name}: ${kvp.Value.Price}");
            }

            // Random selection
            var random = collection.Random();
            Console.WriteLine($"Random product: {random?.Name}");

            // Convert to arrays
            var names = collection.Map(p => p.Name);
            Console.WriteLine($"Product names: {string.Join(", ", names)}");
        }

        /// <summary>
        /// Thread-safe concurrent access example
        /// </summary>
        public static async Task ConcurrentAccessExample()
        {
            var sharedCache = new CacheManager<int, string>(
                fetcher: async (id) =>
                {
                    await Task.Delay(100); // Simulate slow API
                    return $"Data_{id}";
                },
                maxSize: 100
            );

            // Multiple concurrent fetches of the same key
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => sharedCache.FetchAsync(1))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Console.WriteLine($"All results identical: {results.All(r => r == results[0])}");
            Console.WriteLine($"Cache hit count: {sharedCache.Count}"); // Should be 1
        }

        #region Helper Classes

        public class User
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class Message
        {
            public string Id { get; set; }
            public string Content { get; set; }
            public bool IsRead { get; set; }
        }

        public class Product
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
        }

        #endregion
    }
}
