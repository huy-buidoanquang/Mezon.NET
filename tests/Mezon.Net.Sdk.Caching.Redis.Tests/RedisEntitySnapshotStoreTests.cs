using System.Collections.Concurrent;
using Mezon.Net.Sdk.Caching;
using Mezon.Net.Sdk.Caching.Redis.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Mezon.Net.Sdk.Caching.Redis.Tests;

public sealed class RedisEntitySnapshotStoreTests
{
    [Fact]
    public async Task GetAsync_returns_default_when_key_missing()
    {
        var redis = new FakeRedisSnapshotOperations();
        var store = CreateStore(redis);

        var result = await store.GetAsync<TestDto>(SampleKey());

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_and_GetAsync_round_trip_dto()
    {
        var redis = new FakeRedisSnapshotOperations();
        var store = CreateStore(redis);
        var dto = new TestDto { Name = "general", Revision = 3 };

        await store.SetAsync(SampleKey(), dto, CacheEntryOptions.Default);
        var cached = await store.GetAsync<TestDto>(SampleKey());

        Assert.NotNull(cached);
        Assert.Equal("general", cached!.Name);
        Assert.Equal(3, cached.Revision);
    }

    [Fact]
    public async Task SetAsync_with_revision_skips_stale_write()
    {
        var redis = new FakeRedisSnapshotOperations();
        var store = CreateStore(redis);
        var key = SampleKey();

        await store.SetAsync(key, new TestDto { Name = "v2", Revision = 2 }, new CacheEntryOptions { Revision = 2 });
        await store.SetAsync(key, new TestDto { Name = "stale", Revision = 1 }, new CacheEntryOptions { Revision = 1 });

        var cached = await store.GetAsync<TestDto>(key);

        Assert.Equal("v2", cached!.Name);
    }

    [Fact]
    public async Task InvalidateAsync_removes_snapshot()
    {
        var redis = new FakeRedisSnapshotOperations();
        var store = CreateStore(redis);
        var key = SampleKey();

        await store.SetAsync(key, new TestDto { Name = "gone" }, CacheEntryOptions.Default);
        await store.InvalidateAsync(key);

        Assert.Null(await store.GetAsync<TestDto>(key));
    }

    [Fact]
    public async Task GetAsync_returns_default_when_redis_throws()
    {
        var redis = new FakeRedisSnapshotOperations { ThrowOnGet = true };
        var store = CreateStore(redis);

        var result = await store.GetAsync<TestDto>(SampleKey());

        Assert.Null(result);
    }

    [Fact]
    public async Task InvalidateAsync_publishes_when_channel_configured()
    {
        var redis = new FakeRedisSnapshotOperations();
        var store = new RedisEntitySnapshotStore(
            redis,
            new RedisEntitySnapshotStoreOptions { InvalidationChannel = "mezon:test:invalidate" },
            NullLogger<RedisEntitySnapshotStore>.Instance,
            new RedisCacheInvalidationNotifier(redis, "mezon:test:invalidate"));

        await store.SetAsync(SampleKey(), new TestDto { Name = "x" }, CacheEntryOptions.Default);
        await store.InvalidateAsync(SampleKey());

        Assert.Single(redis.PublishedMessages);
        Assert.Equal("prod:42:channel:9001", redis.PublishedMessages[0].Message);
    }

    private static RedisEntitySnapshotStore CreateStore(FakeRedisSnapshotOperations redis) =>
        new(redis, new RedisEntitySnapshotStoreOptions(), NullLogger<RedisEntitySnapshotStore>.Instance);

    private static CacheKey SampleKey() => new("prod", 42, "channel", "9001");

    private sealed class TestDto
    {
        public string Name { get; set; } = string.Empty;

        public long Revision { get; set; }
    }

    private sealed class FakeRedisSnapshotOperations : IRedisSnapshotOperations
    {
        private readonly ConcurrentDictionary<string, RedisValue> _strings = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, long> _revisions = new(StringComparer.Ordinal);

        public bool ThrowOnGet { get; set; }

        public List<(RedisChannel Channel, RedisValue Message)> PublishedMessages { get; } = new();

        public Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            if (ThrowOnGet)
            {
                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "simulated");
            }

            _strings.TryGetValue(key!, out var value);
            return Task.FromResult(value);
        }

        public Task<bool> StringSetAsync(
            RedisKey key,
            RedisValue value,
            TimeSpan? expiry = null,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            _strings[key!] = value;
            return Task.FromResult(true);
        }

        public Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None) =>
            Task.FromResult(_strings.TryRemove(key!, out _));

        public Task<RedisResult> ScriptEvaluateAsync(
            string script,
            RedisKey[]? keys = null,
            RedisValue[]? values = null,
            CommandFlags flags = CommandFlags.None)
        {
            keys ??= Array.Empty<RedisKey>();
            values ??= Array.Empty<RedisValue>();

            var dataKey = keys[0]!;
            var revKey = keys[1]!;
            var newRev = (long)values[0];
            var payload = values[1];

            var currentRev = _revisions.GetValueOrDefault(revKey, 0L);
            if (newRev <= currentRev)
            {
                return Task.FromResult(RedisResult.Create(0));
            }

            _revisions[revKey] = newRev;
            _strings[dataKey] = payload;
            return Task.FromResult(RedisResult.Create(1));
        }

        public Task<long> PublishAsync(RedisChannel channel, RedisValue message, CommandFlags flags = CommandFlags.None)
        {
            PublishedMessages.Add((channel, message));
            return Task.FromResult(1L);
        }

        public Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler) =>
            Task.CompletedTask;

        public Task UnsubscribeAsync(RedisChannel channel) =>
            Task.CompletedTask;
    }
}
