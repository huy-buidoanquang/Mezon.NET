using Mezon.Net.Sdk.Caching.Redis.Internal;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mezon.Net.Sdk.Caching.Redis;

internal sealed class RedisCacheInvalidationNotifier : ICacheInvalidationNotifier
{
    private readonly IRedisSnapshotOperations _redis;
    private readonly RedisChannel _channel;

    public RedisCacheInvalidationNotifier(IRedisSnapshotOperations redis, string channelName)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        if (string.IsNullOrWhiteSpace(channelName))
        {
            throw new ArgumentException("Channel name is required.", nameof(channelName));
        }

        _channel = RedisChannel.Literal(channelName);
    }

    public async ValueTask NotifyInvalidatedAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.PublishAsync(_channel, key.ToRedisKey()).ConfigureAwait(false);
    }
}

/// <summary>
///     Subscribes to Redis pub/sub invalidations and raises <see cref="Invalidated"/>.
/// </summary>
public sealed class RedisCacheInvalidationListener : ICacheInvalidationListener
{
    private readonly IRedisSnapshotOperations _redis;
    private readonly RedisChannel _channel;
    private readonly ILogger<RedisCacheInvalidationListener> _logger;
    private int _started;

    public RedisCacheInvalidationListener(
        IConnectionMultiplexer multiplexer,
        RedisEntitySnapshotStoreOptions options,
        ILogger<RedisCacheInvalidationListener> logger)
        : this(new RedisSnapshotOperations(multiplexer), options, logger)
    {
    }

    internal RedisCacheInvalidationListener(
        IRedisSnapshotOperations redis,
        RedisEntitySnapshotStoreOptions options,
        ILogger<RedisCacheInvalidationListener> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(options.InvalidationChannel))
        {
            throw new InvalidOperationException("InvalidationChannel must be configured to use RedisCacheInvalidationListener.");
        }

        _channel = RedisChannel.Literal(options.InvalidationChannel);
    }

    public event EventHandler<CacheKey>? Invalidated;

    public async ValueTask StartListeningAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        await _redis.SubscribeAsync(_channel, OnMessage).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _started, 0, 1) == 1)
        {
            await _redis.UnsubscribeAsync(_channel).ConfigureAwait(false);
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue message)
    {
        if (message.IsNullOrEmpty)
        {
            return;
        }

        try
        {
            var key = CacheKey.Parse(message!);
            Invalidated?.Invoke(this, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid snapshot invalidation message on {Channel}.", channel);
        }
    }
}
