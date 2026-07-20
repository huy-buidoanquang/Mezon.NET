using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     L2 store for immutable DTO snapshots. Implementations must never persist live entities,
    ///     sockets, sessions, locks, or other process-local state.
    /// </summary>
    public interface IEntitySnapshotStore
    {
        ValueTask<TDto?> GetAsync<TDto>(CacheKey key, CancellationToken cancellationToken = default);

        ValueTask SetAsync<TDto>(
            CacheKey key,
            TDto dto,
            CacheEntryOptions options,
            CancellationToken cancellationToken = default);

        ValueTask InvalidateAsync(CacheKey key, CancellationToken cancellationToken = default);
    }
}
