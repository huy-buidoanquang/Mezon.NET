using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.SDK.Abstractions;

namespace Mezon.Net.SDK
{
    /// <summary>
    /// A generic cache manager that stores data in memory with automatic fetching capability,
    /// size limits, and comprehensive collection operations.
    /// Thread-safe implementation using SemaphoreSlim for async operations.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache</typeparam>
    /// <typeparam name="TValue">The type of values in the cache</typeparam>
    public class CacheManager<TKey, TValue> : ICacheManager<TKey, TValue>
    {
        private readonly Collection<TKey, TValue> _cache;
        private readonly Func<TKey, Task<TValue>> _fetcher;
        private readonly int _maxSize;
        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Initializes a new instance of the CacheManager class
        /// </summary>
        /// <param name="fetcher">Function to fetch values when not in cache</param>
        /// <param name="maxSize">Maximum number of items to store (default: unlimited)</param>
        public CacheManager(Func<TKey, Task<TValue>> fetcher, int maxSize = int.MaxValue)
        {
            _cache = new Collection<TKey, TValue>();
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _maxSize = maxSize > 0 ? maxSize : throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Gets the internal cache collection for advanced operations
        /// </summary>
        public Collection<TKey, TValue> Cache => _cache;

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Gets a value from the cache without fetching
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The cached value or default if not found</returns>
        public TValue Get(TKey key)
        {
            return _cache.Get(key);
        }

        /// <summary>
        /// Sets a value in the cache. If the cache is full, removes the oldest entry (FIFO eviction).
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value to cache</param>
        public void Set(TKey key, TValue value)
        {
            _semaphore.Wait();
            try
            {
                // If cache is at max size, remove the first (oldest) entry
                if (_cache.Count >= _maxSize && !_cache.Has(key))
                {
                    var firstKey = _cache.FirstKey();
                    if (firstKey != null)
                    {
                        _cache.Delete(firstKey);
                    }
                }

                _cache.Set(key, value);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Sets a value in the cache asynchronously
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value to cache</param>
        public async Task SetAsync(TKey key, TValue value)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // If cache is at max size, remove the first (oldest) entry
                if (_cache.Count >= _maxSize && !_cache.Has(key))
                {
                    var firstKey = _cache.FirstKey();
                    if (firstKey != null)
                    {
                        _cache.Delete(firstKey);
                    }
                }

                _cache.Set(key, value);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Fetches a value from cache or retrieves it using the fetcher function if not cached.
        /// Newly fetched values are automatically cached.
        /// </summary>
        /// <param name="key">The key to fetch</param>
        /// <returns>The cached or fetched value</returns>
        public async Task<TValue> FetchAsync(TKey key)
        {
            // Try to get from cache first (no lock needed for read)
            var existing = _cache.Get(key);
            if (existing != null && !existing.Equals(default(TValue)))
            {
                return existing;
            }

            // Need to fetch - acquire lock
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock (another thread might have fetched it)
                existing = _cache.Get(key);
                if (existing != null && !existing.Equals(default(TValue)))
                {
                    return existing;
                }

                // Fetch the value
                var fetched = await _fetcher(key).ConfigureAwait(false);

                // Cache the fetched value (using internal set to avoid re-acquiring lock)
                if (_cache.Count >= _maxSize && !_cache.Has(key))
                {
                    var firstKey = _cache.FirstKey();
                    if (firstKey != null)
                    {
                        _cache.Delete(firstKey);
                    }
                }
                _cache.Set(key, fetched);

                return fetched;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the first value in the cache
        /// </summary>
        /// <returns>The first value or default if cache is empty</returns>
        public TValue First()
        {
            return _cache.First();
        }

        /// <summary>
        /// Gets the last value in the cache
        /// </summary>
        /// <returns>The last value or default if cache is empty</returns>
        public TValue Last()
        {
            return _cache.Last();
        }

        /// <summary>
        /// Filters cached values based on a predicate
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <returns>A new collection containing matching values</returns>
        public Collection<TKey, TValue> Filter(Func<TValue, bool> predicate)
        {
            return _cache.Filter(predicate);
        }

        /// <summary>
        /// Finds the first value that matches the predicate
        /// </summary>
        /// <param name="predicate">The search predicate</param>
        /// <returns>The first matching value or default if not found</returns>
        public TValue Find(Func<TValue, bool> predicate)
        {
            return _cache.Find(predicate);
        }

        /// <summary>
        /// Maps all cached values to a new form
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="selector">The transformation function</param>
        /// <returns>An array of transformed values</returns>
        public TResult[] Map<TResult>(Func<TValue, TResult> selector)
        {
            return _cache.Map(selector);
        }

        /// <summary>
        /// Gets all values from the cache
        /// </summary>
        /// <returns>An enumerable of all cached values</returns>
        public System.Collections.Generic.IEnumerable<TValue> Values()
        {
            return _cache.Values;
        }

        /// <summary>
        /// Gets all keys from the cache
        /// </summary>
        /// <returns>An enumerable of all cached keys</returns>
        public System.Collections.Generic.IEnumerable<TKey> Keys()
        {
            return _cache.Keys;
        }

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>True if the value was removed; otherwise, false</returns>
        public bool Delete(TKey key)
        {
            _semaphore.Wait();
            try
            {
                return _cache.Delete(key);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Removes a value from the cache asynchronously
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>True if the value was removed; otherwise, false</returns>
        public async Task<bool> DeleteAsync(TKey key)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return _cache.Delete(key);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists; otherwise, false</returns>
        public bool Has(TKey key)
        {
            return _cache.Has(key);
        }

        /// <summary>
        /// Clears all cached values
        /// </summary>
        public void Clear()
        {
            _semaphore.Wait();
            try
            {
                _cache.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Clears all cached values asynchronously
        /// </summary>
        public async Task ClearAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                _cache.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets a random value from the cache
        /// </summary>
        /// <returns>A random value or default if cache is empty</returns>
        public TValue Random()
        {
            return _cache.Random();
        }

        /// <summary>
        /// Checks if any cached value matches the predicate
        /// </summary>
        /// <param name="predicate">The predicate to test</param>
        /// <returns>True if any value matches; otherwise, false</returns>
        public bool Some(Func<TValue, bool> predicate)
        {
            return _cache.Some(predicate);
        }

        /// <summary>
        /// Checks if all cached values match the predicate
        /// </summary>
        /// <param name="predicate">The predicate to test</param>
        /// <returns>True if all values match; otherwise, false</returns>
        public bool Every(Func<TValue, bool> predicate)
        {
            return _cache.Every(predicate);
        }

        void ICacheManager<TKey, TValue>.Delete(TKey key)
        {
            Delete(key);
        }
    }
}
