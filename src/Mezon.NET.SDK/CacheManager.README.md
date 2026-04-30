# CacheManager and Collection - C# Implementation

A high-performance, thread-safe cache management system for .NET Standard 2.1, based on TypeScript collection patterns but enhanced with C# best practices.

## Overview

This implementation provides two main components:

1. **Collection<TKey, TValue>** - An enhanced dictionary with utility methods for functional programming patterns
2. **CacheManager<TKey, TValue>** - A thread-safe cache with automatic fetching and size management

## Key Improvements Over TypeScript Version

### 1. **Thread Safety**
- Uses `SemaphoreSlim` for async-friendly thread synchronization
- Double-check locking pattern to minimize lock contention
- Safe concurrent access from multiple threads

### 2. **Type Safety**
- Strong typing with generics (no `any` types)
- Compile-time type checking
- Better IDE intellisense support

### 3. **Performance Optimizations**
- Direct dictionary access instead of iterator patterns where possible
- Efficient array pre-allocation in Map operations
- Minimal boxing/unboxing

### 4. **Memory Management**
- FIFO eviction policy when cache reaches max size
- Configurable cache size limits
- Proper disposal patterns with SemaphoreSlim

### 5. **Enhanced API**
- Additional overloads for filter/map operations with key access
- Async/await support throughout
- Both sync and async methods for flexibility
- Additional utility methods (Last, LastKey, Random, Sort)

## Usage Examples

### Basic Cache Usage

```csharp
var userCache = new CacheManager<string, User>(
    fetcher: async (userId) => await _api.GetUserAsync(userId),
    maxSize: 100
);

// Fetch from cache or API
var user = await userCache.FetchAsync("user_123");

// Get from cache only (no fetch)
var cachedUser = userCache.Get("user_123");

// Manual set
userCache.Set("user_456", newUser);
```

### Filtering and Mapping

```csharp
// Filter active users
var activeUsers = userCache.Filter(user => user.IsActive);

// Map to user names
string[] names = userCache.Map(user => user.Name);

// Find specific user
var admin = userCache.Find(user => user.Role == "Admin");
```

### Collection Operations

```csharp
var collection = new Collection<string, Product>();

collection
    .Set("prod_1", product1)
    .Set("prod_2", product2)
    .Set("prod_3", product3);

// Functional operations
var total = collection.Reduce(0m, (sum, product) => sum + product.Price);
var expensive = collection.Filter(p => p.Price > 100);
var sorted = collection.Sort((a, b) => a.Price.CompareTo(b.Price));
```

### Cache Eviction

```csharp
var limitedCache = new CacheManager<int, string>(
    fetcher: async (id) => await FetchDataAsync(id),
    maxSize: 5
);

// When 6th item is added, first (oldest) item is automatically removed
for (int i = 1; i <= 6; i++)
{
    await limitedCache.FetchAsync(i);
}

// Item 1 has been evicted, items 2-6 remain
```

## Architecture Decisions

### Why Keep the Collection Class?

While C# has excellent built-in collections, we kept the `Collection<TKey, TValue>` class for several reasons:

1. **Consistent API** - Maintains similar method signatures to the TypeScript version
2. **Functional Programming** - Provides LINQ-like operations in a more discoverable API
3. **Chainable Operations** - Fluent interface for multiple operations
4. **Migration Path** - Easier for TypeScript developers to transition

### Alternative Approach (Without Collection)

If you prefer to use standard C# types, you could implement CacheManager directly with `Dictionary<TKey, TValue>` and rely on LINQ for operations:

```csharp
// Without Collection class
private readonly Dictionary<TKey, TValue> _cache;

public TValue[] Map<TResult>(Func<TValue, TResult> selector)
{
    return _cache.Values.Select(selector).ToArray();
}
```

However, the Collection class provides:
- Consistent API surface
- Method chaining capabilities
- Easier discoverability for developers
- Better separation of concerns

## Performance Considerations

### Time Complexity
- Get/Set/Has: O(1) average case
- Filter/Map/Find: O(n)
- Sort: O(n log n)
- Eviction: O(1) with FIFO

### Memory Usage
- Each cache entry: Key + Value + Dictionary overhead (~32 bytes per entry on 64-bit)
- SemaphoreSlim: ~100 bytes
- Collection wrapper: Minimal overhead

### Concurrency
- Lock-free reads for most operations
- Write operations protected by SemaphoreSlim
- Minimal lock contention with double-check pattern

## Thread Safety Guarantees

- **Get operations**: Thread-safe, lock-free
- **Set operations**: Thread-safe with lock
- **FetchAsync**: Thread-safe with deduplication (same key fetched once even with concurrent calls)
- **Collection enumeration**: Safe for read-only operations during writes

## API Reference

### CacheManager<TKey, TValue>

| Method | Description | Returns |
|--------|-------------|---------|
| `Get(key)` | Get from cache without fetching | `TValue` |
| `Set(key, value)` | Add or update cache entry | `void` |
| `FetchAsync(key)` | Get from cache or fetch if missing | `Task<TValue>` |
| `Delete(key)` | Remove from cache | `bool` |
| `Has(key)` | Check if key exists | `bool` |
| `Clear()` | Remove all entries | `void` |
| `Filter(predicate)` | Filter cached values | `Collection<TKey, TValue>` |
| `Map<TResult>(selector)` | Transform all values | `TResult[]` |
| `Find(predicate)` | Find first matching value | `TValue` |
| `First()` | Get first value | `TValue` |
| `Last()` | Get last value | `TValue` |
| `Random()` | Get random value | `TValue` |
| `Some(predicate)` | Check if any match | `bool` |
| `Every(predicate)` | Check if all match | `bool` |
| `Values()` | Get all values | `IEnumerable<TValue>` |
| `Keys()` | Get all keys | `IEnumerable<TKey>` |

### Collection<TKey, TValue>

Extends the CacheManager methods with additional utility operations and provides the underlying storage mechanism.

## Integration with Mezon.Net.SDK

This implementation is designed to work seamlessly with the Mezon SDK:

```csharp
public interface IMezonClient
{
    ICacheManager<string, IClan> Clans { get; }
    ICacheManager<string, ITextChannel> Channels { get; }
}

// Usage
var client = new MezonClient();
var clan = await client.Clans.FetchAsync("clan_123");
var channels = client.Channels.Filter(ch => ch.ClanId == "clan_123");
```

## Future Enhancements

Potential improvements for future versions:

1. **LRU Eviction** - Add Least Recently Used eviction policy option
2. **TTL Support** - Automatic expiration based on time-to-live
3. **Cache Statistics** - Hit/miss rates, eviction counts
4. **Serialization** - Persist cache to disk
5. **Distributed Caching** - Redis/Memcached backing
6. **Weak References** - Allow GC to collect under memory pressure

## License

Part of the Mezon.Net SDK project.
