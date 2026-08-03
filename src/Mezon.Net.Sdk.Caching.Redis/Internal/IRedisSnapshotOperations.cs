using StackExchange.Redis;

namespace Mezon.Net.Sdk.Caching.Redis.Internal;

internal interface IRedisSnapshotOperations
{
    Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    Task<bool> StringSetAsync(
        RedisKey key,
        RedisValue value,
        TimeSpan? expiry = null,
        When when = When.Always,
        CommandFlags flags = CommandFlags.None);

    Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    Task<RedisResult> ScriptEvaluateAsync(
        string script,
        RedisKey[]? keys = null,
        RedisValue[]? values = null,
        CommandFlags flags = CommandFlags.None);

    Task<long> PublishAsync(RedisChannel channel, RedisValue message, CommandFlags flags = CommandFlags.None);

    Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler);

    Task UnsubscribeAsync(RedisChannel channel);
}
