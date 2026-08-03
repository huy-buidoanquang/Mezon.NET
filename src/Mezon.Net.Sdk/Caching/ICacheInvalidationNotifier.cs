using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Optional hook invoked when a snapshot key is invalidated so other nodes can drop L1 copies.
    /// </summary>
    public interface ICacheInvalidationNotifier
    {
        ValueTask NotifyInvalidatedAsync(CacheKey key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Optional listener for cross-node snapshot invalidations published by <see cref="ICacheInvalidationNotifier"/>.
    /// </summary>
    public interface ICacheInvalidationListener : IAsyncDisposable
    {
        event EventHandler<CacheKey>? Invalidated;

        ValueTask StartListeningAsync(CancellationToken cancellationToken = default);
    }
}
