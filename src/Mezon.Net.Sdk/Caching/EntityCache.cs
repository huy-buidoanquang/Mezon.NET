using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Process-local identity map with hard capacity (LRU eviction) and single-flight fetch.
    /// </summary>
    public sealed class EntityCache<T> where T : class
    {
        private readonly object _lruGate = new object();
        private readonly Dictionary<long, LinkedListNode<CacheEntry>> _map = new Dictionary<long, LinkedListNode<CacheEntry>>();
        private readonly LinkedList<CacheEntry> _lru = new LinkedList<CacheEntry>();
        private readonly ConcurrentDictionary<long, Lazy<Task<T>>> _inflight = new ConcurrentDictionary<long, Lazy<Task<T>>>();
        private readonly int _capacity;

        public EntityCache(int capacity = 1000)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Count
        {
            get
            {
                lock (_lruGate)
                {
                    return _map.Count;
                }
            }
        }

        public int Capacity => _capacity;

        public T? Get(long id)
        {
            lock (_lruGate)
            {
                if (!_map.TryGetValue(id, out var node))
                {
                    return null;
                }

                Touch(node);
                return node.Value.Entity;
            }
        }

        public bool TryGet(long id, [NotNullWhen(true)] out T? entity)
        {
            entity = Get(id);
            return entity != null;
        }

        public void Set(long id, T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            lock (_lruGate)
            {
                if (_map.TryGetValue(id, out var existing))
                {
                    existing.Value.Entity = entity;
                    Touch(existing);
                    return;
                }

                var entry = new CacheEntry(id, entity);
                var node = _lru.AddFirst(entry);
                _map[id] = node;
                EvictOverflow();
            }
        }

        public bool Remove(long id)
        {
            lock (_lruGate)
            {
                if (!_map.TryGetValue(id, out var node))
                {
                    return false;
                }

                _lru.Remove(node);
                _map.Remove(id);
                return true;
            }
        }

        public void Clear()
        {
            lock (_lruGate)
            {
                _map.Clear();
                _lru.Clear();
            }

            _inflight.Clear();
        }

        /// <summary>
        ///     Returns a cached entity or runs <paramref name="factory"/> once per id (single-flight).
        /// </summary>
        public async ValueTask<T> GetOrFetchAsync(
            long id,
            Func<long, CancellationToken, ValueTask<T>> factory,
            CancellationToken cancellationToken = default)
        {
            var cached = Get(id);
            if (cached != null)
            {
                return cached;
            }

            var lazy = _inflight.GetOrAdd(
                id,
                key => new Lazy<Task<T>>(
                    () => FetchAndCacheAsync(key, factory, cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return await lazy.Value.ConfigureAwait(false);
            }
            finally
            {
                _inflight.TryRemove(id, out _);
            }
        }

        private async Task<T> FetchAndCacheAsync(
            long id,
            Func<long, CancellationToken, ValueTask<T>> factory,
            CancellationToken cancellationToken)
        {
            var existing = Get(id);
            if (existing != null)
            {
                return existing;
            }

            var entity = await factory(id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Entity factory for id {id} returned null.");
            Set(id, entity);
            return entity;
        }

        private void Touch(LinkedListNode<CacheEntry> node)
        {
            if (node.List != _lru || node == _lru.First)
            {
                return;
            }

            _lru.Remove(node);
            _lru.AddFirst(node);
        }

        private void EvictOverflow()
        {
            while (_map.Count > _capacity && _lru.Last != null)
            {
                var last = _lru.Last;
                _lru.RemoveLast();
                _map.Remove(last.Value.Id);
            }
        }

        private sealed class CacheEntry
        {
            public CacheEntry(long id, T entity)
            {
                Id = id;
                Entity = entity;
            }

            public long Id { get; }
            public T Entity { get; set; }
        }
    }
}
