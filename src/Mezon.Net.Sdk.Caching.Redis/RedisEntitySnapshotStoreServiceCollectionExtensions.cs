using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mezon.Net.Sdk.Caching.Redis;

public static class RedisEntitySnapshotStoreServiceCollectionExtensions
{
    public static IServiceCollection AddMezonRedisEntitySnapshotStore(
        this IServiceCollection services,
        IConnectionMultiplexer multiplexer,
        Action<RedisEntitySnapshotStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(multiplexer);

        var options = new RedisEntitySnapshotStoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IEntitySnapshotStore>(provider =>
            new RedisEntitySnapshotStore(
                multiplexer,
                provider.GetRequiredService<RedisEntitySnapshotStoreOptions>(),
                provider.GetRequiredService<ILogger<RedisEntitySnapshotStore>>()));

        return services;
    }

    public static IServiceCollection AddMezonRedisSnapshotInvalidationListener(
        this IServiceCollection services,
        IConnectionMultiplexer multiplexer,
        Action<RedisEntitySnapshotStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(multiplexer);

        var options = new RedisEntitySnapshotStoreOptions();
        configure?.Invoke(options);

        if (string.IsNullOrWhiteSpace(options.InvalidationChannel))
        {
            throw new InvalidOperationException("InvalidationChannel must be configured for snapshot invalidation listening.");
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<ICacheInvalidationListener>(provider =>
            new RedisCacheInvalidationListener(
                multiplexer,
                provider.GetRequiredService<RedisEntitySnapshotStoreOptions>(),
                provider.GetRequiredService<ILogger<RedisCacheInvalidationListener>>()));

        return services;
    }
}
