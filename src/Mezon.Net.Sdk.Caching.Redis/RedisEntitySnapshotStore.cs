using System.Text.Json;
using Mezon.Net.Sdk.Caching.Redis.Internal;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mezon.Net.Sdk.Caching.Redis;

/// <summary>
///     Redis/Valkey-backed L2 store for DTO snapshots only.
///     Redis failures are treated as cache misses at the store boundary.
/// </summary>
public sealed class RedisEntitySnapshotStore : IEntitySnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IRedisSnapshotOperations _redis;
    private readonly RedisEntitySnapshotStoreOptions _options;
    private readonly ILogger<RedisEntitySnapshotStore> _logger;
    private readonly ICacheInvalidationNotifier? _invalidationNotifier;

    public RedisEntitySnapshotStore(
        IConnectionMultiplexer multiplexer,
        RedisEntitySnapshotStoreOptions options,
        ILogger<RedisEntitySnapshotStore> logger)
    {
        _ = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));

        var redis = new RedisSnapshotOperations(multiplexer);
        _redis = redis;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _invalidationNotifier = CreateInvalidationNotifier(options, redis);
    }

    internal RedisEntitySnapshotStore(
        IRedisSnapshotOperations redis,
        RedisEntitySnapshotStoreOptions options,
        ILogger<RedisEntitySnapshotStore> logger,
        ICacheInvalidationNotifier? invalidationNotifier = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _invalidationNotifier = invalidationNotifier;
    }

    public async ValueTask<TDto?> GetAsync<TDto>(CacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = await _redis.StringGetAsync(ToDataKey(key)).ConfigureAwait(false);
            if (payload.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<TDto>((string)payload!, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis snapshot GET failed for {CacheKey}; treating as cache miss.", key);
            return default;
        }
    }

    public async ValueTask SetAsync<TDto>(
        CacheKey key,
        TDto dto,
        CacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        _ = dto ?? throw new ArgumentNullException(nameof(dto));
        _ = options ?? throw new ArgumentNullException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = JsonSerializer.Serialize(dto, SerializerOptions);
            var expiry = ResolveExpiry(options);

            if (options.Revision is long revision)
            {
                var keys = new[] { ToDataKey(key), ToRevisionKey(key) };
                var values = new RedisValue[]
                {
                    revision,
                    payload,
                    expiry.HasValue ? (long)expiry.Value.TotalSeconds : 0L
                };

                await _redis.ScriptEvaluateAsync(RedisSnapshotScripts.CompareAndSet, keys, values).ConfigureAwait(false);
                return;
            }

            await _redis.StringSetAsync(ToDataKey(key), payload, expiry).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis snapshot SET failed for {CacheKey}; ignoring.", key);
        }
    }

    public async ValueTask InvalidateAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _redis.KeyDeleteAsync(ToDataKey(key)).ConfigureAwait(false);
            await _redis.KeyDeleteAsync(ToRevisionKey(key)).ConfigureAwait(false);

            if (_invalidationNotifier is not null)
            {
                await _invalidationNotifier.NotifyInvalidatedAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis snapshot INVALIDATE failed for {CacheKey}; ignoring.", key);
        }
    }

    private RedisKey ToDataKey(CacheKey key)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.KeyPrefix) ? string.Empty : _options.KeyPrefix + ":";
        return prefix + key.ToRedisKey();
    }

    private RedisKey ToRevisionKey(CacheKey key)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.KeyPrefix) ? string.Empty : _options.KeyPrefix + ":";
        return prefix + key.ToRedisKey() + ":rev";
    }

    private TimeSpan? ResolveExpiry(CacheEntryOptions options) =>
        options.AbsoluteExpirationRelativeToNow ?? _options.DefaultAbsoluteExpirationRelativeToNow;

    private static ICacheInvalidationNotifier? CreateInvalidationNotifier(
        RedisEntitySnapshotStoreOptions options,
        IRedisSnapshotOperations redis)
    {
        if (string.IsNullOrWhiteSpace(options.InvalidationChannel))
        {
            return null;
        }

        return new RedisCacheInvalidationNotifier(redis, options.InvalidationChannel);
    }
}
