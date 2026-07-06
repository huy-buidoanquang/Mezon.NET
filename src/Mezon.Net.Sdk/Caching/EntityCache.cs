using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    public sealed class EntityCache<T> where T : class
    {
        private readonly ConcurrentDictionary<long, T> _items;
        private readonly int _capacity;

        public EntityCache(int capacity = 512)
        {
            _capacity = Math.Max(16, capacity);
            _items = new ConcurrentDictionary<long, T>(concurrencyLevel: Environment.ProcessorCount, capacity: _capacity);
        }

        public int Count => _items.Count;

        public bool TryGet(long id, [NotNullWhen(true)] out T? entity) => _items.TryGetValue(id, out entity);

        public void Set(long id, T entity) => _items[id] = entity;

        public bool Remove(long id) => _items.TryRemove(id, out _);

        public IEnumerable<T> Values => _items.Values;

        public ValueTask<T> GetOrFetchAsync(long id, Func<long, CancellationToken, ValueTask<T>> factory, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(id, out var existing))
            {
                return new ValueTask<T>(existing);
            }

            return new ValueTask<T>(FetchSlowAsync(id, factory, cancellationToken));
        }

        private async Task<T> FetchSlowAsync(long id, Func<long, CancellationToken, ValueTask<T>> factory, CancellationToken cancellationToken)
        {
            if (_items.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var created = await factory(id, cancellationToken).ConfigureAwait(false);
            return _items.GetOrAdd(id, created);
        }
    }
}
