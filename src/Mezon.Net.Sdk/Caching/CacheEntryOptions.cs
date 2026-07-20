using System;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Snapshot write options for an <see cref="IEntitySnapshotStore"/> entry.
    /// </summary>
    public sealed class CacheEntryOptions
    {
        public static CacheEntryOptions Default { get; } = new CacheEntryOptions();

        /// <summary>
        ///     Optional relative expiration applied by the backing store.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; init; }

        /// <summary>
        ///     Optional monotonic revision. Stores that support compare-and-swap skip stale writes.
        /// </summary>
        public long? Revision { get; init; }
    }
}
