using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.NET.SDK
{
    /// <summary>
    /// A specialized collection class that extends Dictionary with additional utility methods
    /// for working with cached data in a more expressive way.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the collection</typeparam>
    /// <typeparam name="TValue">The type of values in the collection</typeparam>
    public class Collection<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _innerDictionary;

        /// <summary>
        /// Initializes a new instance of the Collection class
        /// </summary>
        public Collection()
        {
            _innerDictionary = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// Initializes a new instance of the Collection class with the specified entries
        /// </summary>
        /// <param name="entries">Initial key-value pairs</param>
        public Collection(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            _innerDictionary = entries != null
                ? new Dictionary<TKey, TValue>(entries.ToDictionary(x => x.Key, x => x.Value))
                : new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// Gets the number of elements in the collection
        /// </summary>
        public int Count => _innerDictionary.Count;

        /// <summary>
        /// Adds or updates a key-value pair in the collection
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>The collection instance for method chaining</returns>
        public Collection<TKey, TValue> Set(TKey key, TValue value)
        {
            _innerDictionary[key] = value;
            return this;
        }

        /// <summary>
        /// Removes an element with the specified key
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>True if the element was removed successfully; otherwise, false</returns>
        public bool Delete(TKey key)
        {
            return _innerDictionary.Remove(key);
        }

        /// <summary>
        /// Determines whether the collection contains the specified key
        /// </summary>
        /// <param name="key">The key to locate</param>
        /// <returns>True if the collection contains an element with the key; otherwise, false</returns>
        public bool Has(TKey key)
        {
            return _innerDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Gets the value associated with the specified key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The value if found; otherwise, default(TValue)</returns>
        public TValue Get(TKey key)
        {
            return _innerDictionary.TryGetValue(key, out var value) ? value : default;
        }

        /// <summary>
        /// Gets the first value in the collection
        /// </summary>
        /// <returns>The first value or default if empty</returns>
        public TValue First()
        {
            return _innerDictionary.Values.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first key in the collection
        /// </summary>
        /// <returns>The first key or default if empty</returns>
        public TKey FirstKey()
        {
            return _innerDictionary.Keys.FirstOrDefault();
        }

        /// <summary>
        /// Gets the last value in the collection
        /// </summary>
        /// <returns>The last value or default if empty</returns>
        public TValue Last()
        {
            return _innerDictionary.Values.LastOrDefault();
        }

        /// <summary>
        /// Gets the last key in the collection
        /// </summary>
        /// <returns>The last key or default if empty</returns>
        public TKey LastKey()
        {
            return _innerDictionary.Keys.LastOrDefault();
        }

        /// <summary>
        /// Filters the collection based on a predicate
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>A new collection containing only elements that satisfy the predicate</returns>
        public Collection<TKey, TValue> Filter(Func<TValue, bool> predicate)
        {
            var filtered = new Collection<TKey, TValue>();
            foreach (var kvp in _innerDictionary)
            {
                if (predicate(kvp.Value))
                {
                    filtered.Set(kvp.Key, kvp.Value);
                }
            }
            return filtered;
        }

        /// <summary>
        /// Filters the collection based on a predicate with key and value
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>A new collection containing only elements that satisfy the predicate</returns>
        public Collection<TKey, TValue> Filter(Func<TValue, TKey, bool> predicate)
        {
            var filtered = new Collection<TKey, TValue>();
            foreach (var kvp in _innerDictionary)
            {
                if (predicate(kvp.Value, kvp.Key))
                {
                    filtered.Set(kvp.Key, kvp.Value);
                }
            }
            return filtered;
        }

        /// <summary>
        /// Finds the first value that satisfies the predicate
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>The first matching value or default if not found</returns>
        public TValue Find(Func<TValue, bool> predicate)
        {
            foreach (var kvp in _innerDictionary)
            {
                if (predicate(kvp.Value))
                {
                    return kvp.Value;
                }
            }
            return default;
        }

        /// <summary>
        /// Finds the first value that satisfies the predicate with key and value
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>The first matching value or default if not found</returns>
        public TValue Find(Func<TValue, TKey, bool> predicate)
        {
            foreach (var kvp in _innerDictionary)
            {
                if (predicate(kvp.Value, kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return default;
        }

        /// <summary>
        /// Projects each element to a new form
        /// </summary>
        /// <typeparam name="TResult">The type of the resulting elements</typeparam>
        /// <param name="selector">The transformation function</param>
        /// <returns>An array of transformed elements</returns>
        public TResult[] Map<TResult>(Func<TValue, TResult> selector)
        {
            var result = new TResult[_innerDictionary.Count];
            int index = 0;
            foreach (var kvp in _innerDictionary)
            {
                result[index++] = selector(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Projects each element to a new form with key and value
        /// </summary>
        /// <typeparam name="TResult">The type of the resulting elements</typeparam>
        /// <param name="selector">The transformation function</param>
        /// <returns>An array of transformed elements</returns>
        public TResult[] Map<TResult>(Func<TValue, TKey, TResult> selector)
        {
            var result = new TResult[_innerDictionary.Count];
            int index = 0;
            foreach (var kvp in _innerDictionary)
            {
                result[index++] = selector(kvp.Value, kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Determines whether any element satisfies the predicate
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>True if any element satisfies the predicate; otherwise, false</returns>
        public bool Some(Func<TValue, bool> predicate)
        {
            return _innerDictionary.Values.Any(predicate);
        }

        /// <summary>
        /// Determines whether all elements satisfy the predicate
        /// </summary>
        /// <param name="predicate">The predicate to test each element</param>
        /// <returns>True if all elements satisfy the predicate; otherwise, false</returns>
        public bool Every(Func<TValue, bool> predicate)
        {
            return _innerDictionary.Values.All(predicate);
        }

        /// <summary>
        /// Applies an accumulator function over the collection
        /// </summary>
        /// <typeparam name="TAccumulate">The type of the accumulator</typeparam>
        /// <param name="seed">The initial accumulator value</param>
        /// <param name="func">The accumulator function</param>
        /// <returns>The final accumulator value</returns>
        public TAccumulate Reduce<TAccumulate>(TAccumulate seed, Func<TAccumulate, TValue, TAccumulate> func)
        {
            var accumulator = seed;
            foreach (var kvp in _innerDictionary)
            {
                accumulator = func(accumulator, kvp.Value);
            }
            return accumulator;
        }

        /// <summary>
        /// Applies an accumulator function over the collection with key and value
        /// </summary>
        /// <typeparam name="TAccumulate">The type of the accumulator</typeparam>
        /// <param name="seed">The initial accumulator value</param>
        /// <param name="func">The accumulator function</param>
        /// <returns>The final accumulator value</returns>
        public TAccumulate Reduce<TAccumulate>(TAccumulate seed, Func<TAccumulate, TValue, TKey, TAccumulate> func)
        {
            var accumulator = seed;
            foreach (var kvp in _innerDictionary)
            {
                accumulator = func(accumulator, kvp.Value, kvp.Key);
            }
            return accumulator;
        }

        /// <summary>
        /// Gets a random value from the collection
        /// </summary>
        /// <returns>A random value or default if empty</returns>
        public TValue Random()
        {
            if (_innerDictionary.Count == 0)
            {
                return default;
            }

            var values = _innerDictionary.Values.ToArray();
            var random = new Random();
            return values[random.Next(values.Length)];
        }

        /// <summary>
        /// Sorts the collection by values using the specified comparer
        /// </summary>
        /// <param name="comparer">The comparison function</param>
        /// <returns>A new sorted collection</returns>
        public Collection<TKey, TValue> Sort(Comparison<TValue> comparer = null)
        {
            var sorted = comparer != null
                ? _innerDictionary.OrderBy(kvp => kvp.Value, Comparer<TValue>.Create(comparer))
                : _innerDictionary.OrderBy(kvp => kvp.Value);

            return new Collection<TKey, TValue>(sorted);
        }

        /// <summary>
        /// Removes all elements from the collection
        /// </summary>
        public void Clear()
        {
            _innerDictionary.Clear();
        }

        /// <summary>
        /// Gets all keys as an array
        /// </summary>
        /// <returns>An array of keys</returns>
        public TKey[] KeysArray()
        {
            return _innerDictionary.Keys.ToArray();
        }

        /// <summary>
        /// Gets all values as an array
        /// </summary>
        /// <returns>An array of values</returns>
        public TValue[] ValuesArray()
        {
            return _innerDictionary.Values.ToArray();
        }

        /// <summary>
        /// Gets all key-value pairs as an array
        /// </summary>
        /// <returns>An array of key-value pairs</returns>
        public KeyValuePair<TKey, TValue>[] EntriesArray()
        {
            return _innerDictionary.ToArray();
        }

        /// <summary>
        /// Gets an enumerable of all keys
        /// </summary>
        public IEnumerable<TKey> Keys => _innerDictionary.Keys;

        /// <summary>
        /// Gets an enumerable of all values
        /// </summary>
        public IEnumerable<TValue> Values => _innerDictionary.Values;

        /// <summary>
        /// Returns an enumerator that iterates through the collection
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _innerDictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
