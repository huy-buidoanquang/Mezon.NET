using System;
using System.Diagnostics.CodeAnalysis;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Read-only filtered view over a shared <see cref="EntityCache{T}"/> identity map.
    /// </summary>
    public sealed class EntityCacheView<T> where T : class
    {
        private readonly EntityCache<T> _inner;
        private readonly Func<T, bool> _predicate;

        internal EntityCacheView(EntityCache<T> inner, Func<T, bool> predicate)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public T? Get(long id)
        {
            if (!_inner.TryGet(id, out var entity) || !_predicate(entity))
            {
                return null;
            }

            return entity;
        }

        public bool TryGet(long id, [NotNullWhen(true)] out T? entity)
        {
            entity = Get(id);
            return entity != null;
        }
    }

}
