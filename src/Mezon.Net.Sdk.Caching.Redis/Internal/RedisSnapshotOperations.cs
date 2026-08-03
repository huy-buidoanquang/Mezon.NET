using StackExchange.Redis;

namespace Mezon.Net.Sdk.Caching.Redis.Internal;

internal sealed class RedisSnapshotOperations : IRedisSnapshotOperations
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;

    public RedisSnapshotOperations(IConnectionMultiplexer multiplexer)
    {
        _ = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        _database = multiplexer.GetDatabase();
        _subscriber = multiplexer.GetSubscriber();
    }

    public Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None) =>
        _database.StringGetAsync(key, flags);

    public Task<bool> StringSetAsync(
        RedisKey key,
        RedisValue value,
        TimeSpan? expiry = null,
        When when = When.Always,
        CommandFlags flags = CommandFlags.None) =>
        _database.StringSetAsync(key, value, expiry, when, flags);

    public Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None) =>
        _database.KeyDeleteAsync(key, flags);

    public Task<RedisResult> ScriptEvaluateAsync(
        string script,
        RedisKey[]? keys = null,
        RedisValue[]? values = null,
        CommandFlags flags = CommandFlags.None) =>
        _database.ScriptEvaluateAsync(script, keys, values, flags);

    public Task<long> PublishAsync(RedisChannel channel, RedisValue message, CommandFlags flags = CommandFlags.None) =>
        _subscriber.PublishAsync(channel, message, flags);

    public Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler) =>
        _subscriber.SubscribeAsync(channel, handler);

    public Task UnsubscribeAsync(RedisChannel channel) =>
        _subscriber.UnsubscribeAsync(channel);
}
