namespace Mezon.Net.Sdk.Caching.Redis;

/// <summary>
///     Configuration for <see cref="RedisEntitySnapshotStore"/>.
/// </summary>
public sealed class RedisEntitySnapshotStoreOptions
{
    /// <summary>
    ///     Optional prefix applied to every Redis key (e.g. <c>mezon:snapshot</c>).
    /// </summary>
    public string KeyPrefix { get; set; } = "mezon:snapshot";

    /// <summary>
    ///     When set, invalidations are published on this Redis channel so other nodes can drop L1 copies.
    /// </summary>
    public string? InvalidationChannel { get; set; }

    /// <summary>
    ///     Default expiration used when <see cref="CacheEntryOptions.AbsoluteExpirationRelativeToNow"/> is not set.
    /// </summary>
    public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; }
}
