namespace Mezon.Net.Sdk.Caching.Redis.Internal;

internal static class RedisSnapshotScripts
{
    /// <summary>
    ///     KEYS[1] = data key, KEYS[2] = revision key.
    ///     ARGV[1] = new revision, ARGV[2] = payload, ARGV[3] = ttl seconds (0 = no expiry).
    ///     Returns 1 when written, 0 when skipped as stale.
    /// </summary>
    public const string CompareAndSet = """
        local currentRev = tonumber(redis.call('GET', KEYS[2]) or '0')
        local newRev = tonumber(ARGV[1])
        if newRev <= currentRev then
          return 0
        end

        redis.call('SET', KEYS[2], newRev)
        redis.call('SET', KEYS[1], ARGV[2])
        local ttl = tonumber(ARGV[3])
        if ttl > 0 then
          redis.call('EXPIRE', KEYS[1], ttl)
          redis.call('EXPIRE', KEYS[2], ttl)
        end
        return 1
        """;
}
