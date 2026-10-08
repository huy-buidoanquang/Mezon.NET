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
        /// <remarks>
        ///     The shared fetch is not tied to any caller: <paramref name="cancellationToken"/> only stops this
        ///     caller's wait, and the factory receives <see cref="CancellationToken.None"/> so one cancelled caller
        ///     cannot fail the others waiting for the same id.
        /// </remarks>
        public ValueTask<T> GetOrFetchAsync(
            long id,
            Func<long, CancellationToken, ValueTask<T>> factory,
            CancellationToken cancellationToken = default)
        {
            var cached = Get(id);
            if (cached != null)
            {
                return new ValueTask<T>(cached);
            }

            return new ValueTask<T>(GetOrFetchSlowAsync(id, factory, cancellationToken));
        }

        private Task<T> GetOrFetchSlowAsync(
            long id,
            Func<long, CancellationToken, ValueTask<T>> factory,
            CancellationToken cancellationToken)
        {
            Lazy<Task<T>>? created = null;
            created = new Lazy<Task<T>>(
                () => FetchAndCacheAsync(id, factory, created!),
                LazyThreadSafetyMode.ExecutionAndPublication);
            var fetch = _inflight.GetOrAdd(id, created).Value;
            return WaitAsync(fetch, cancellationToken);
        }

        private async Task<T> FetchAndCacheAsync(
            long id,
            Func<long, CancellationToken, ValueTask<T>> factory,
            Lazy<Task<T>> self)
        {
            try
            {
                var existing = Get(id);
                if (existing != null)
                {
                    return existing;
                }

                var entity = await factory(id, CancellationToken.None).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Entity factory for id {id} returned null.");
                Set(id, entity);
                return entity;
            }
            finally
            {
                // Remove only this fetch: a newer fetch for the same id may already be registered.
                ((ICollection<KeyValuePair<long, Lazy<Task<T>>>>)_inflight).Remove(new KeyValuePair<long, Lazy<Task<T>>>(id, self));
            }
        }

        private static Task<T> WaitAsync(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

#if NET6_0_OR_GREATER
            return task.WaitAsync(cancellationToken);
#else
            return WaitWithCancellationAsync(task, cancellationToken);
#endif
        }

#if !NET6_0_OR_GREATER
        private static async Task<T> WaitWithCancellationAsync(Task<T> task, CancellationToken cancellationToken)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
            {
                if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }
#endif

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
