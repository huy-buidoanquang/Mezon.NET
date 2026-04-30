using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mezon.Net.SDK.Abstractions
{
    /// <summary>
    /// Interface for cache management with automatic fetching capability
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache</typeparam>
    /// <typeparam name="TValue">The type of values in the cache</typeparam>
    public interface ICacheManager<TKey, TValue>
    {
        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets a value from the cache without fetching
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>The cached value or default if not found</returns>
        TValue Get(TKey key);

        /// <summary>
        /// Sets a value in the cache
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value to cache</param>
        void Set(TKey key, TValue value);

        /// <summary>
        /// Fetches a value from cache or retrieves it using the fetcher function if not cached
        /// </summary>
        /// <param name="key">The key to fetch</param>
        /// <returns>The cached or fetched value</returns>
        Task<TValue> FetchAsync(TKey key);

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">The key to remove</param>
        void Delete(TKey key);

        /// <summary>
        /// Gets the first value in the cache
        /// </summary>
        /// <returns>The first value or default if cache is empty</returns>
        TValue First();

        /// <summary>
        /// Filters cached values based on a predicate
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <returns>A collection containing matching values</returns>
        Collection<TKey, TValue> Filter(Func<TValue, bool> predicate);

        /// <summary>
        /// Maps all cached values to a new form
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="selector">The transformation function</param>
        /// <returns>An array of transformed values</returns>
        TResult[] Map<TResult>(Func<TValue, TResult> selector);

        /// <summary>
        /// Gets all values from the cache
        /// </summary>
        /// <returns>An enumerable of all cached values</returns>
        IEnumerable<TValue> Values();

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists; otherwise, false</returns>
        bool Has(TKey key);

        /// <summary>
        /// Clears all cached values
        /// </summary>
        void Clear();
    }
}
