using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Caching;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class EntityCacheTests
    {
        [Fact]
        public async Task GetOrFetchAsync_returns_cached_instance_without_factory_call()
        {
            var cache = new EntityCache<string>();
            cache.Set(1, "cached");
            var factoryCalls = 0;
            var value = await cache.GetOrFetchAsync(1, (_, __) =>
            {
                factoryCalls++;
                return new ValueTask<string>("new");
            });
            Assert.Equal("cached", value);
            Assert.Equal(0, factoryCalls);
        }

        [Fact]
        public async Task GetOrFetchAsync_single_flight_calls_factory_once()
        {
            var cache = new EntityCache<string>(capacity: 8);
            var calls = 0;
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async ValueTask<string> Factory(long _, CancellationToken __)
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult(true);
                await Task.Delay(50).ConfigureAwait(false);
                return "created";
            }

            var t1 = cache.GetOrFetchAsync(7, Factory).AsTask();
            await started.Task.ConfigureAwait(false);
            var t2 = cache.GetOrFetchAsync(7, Factory).AsTask();
            var results = await Task.WhenAll(t1, t2).ConfigureAwait(false);

            Assert.Equal("created", results[0]);
            Assert.Equal("created", results[1]);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task GetOrFetchAsync_cancelling_one_waiter_does_not_fail_the_others()
        {
            var cache = new EntityCache<string>(capacity: 8);
            var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken factoryToken = default;
            using var firstCaller = new CancellationTokenSource();

            var first = cache.GetOrFetchAsync(7, (_, ct) =>
            {
                factoryToken = ct;
                return new ValueTask<string>(release.Task);
            }, firstCaller.Token).AsTask();
            var second = cache.GetOrFetchAsync(7, (_, __) => new ValueTask<string>("unused")).AsTask();

            firstCaller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            release.SetResult("created");

            Assert.Equal("created", await second);
            Assert.False(factoryToken.CanBeCanceled);
            Assert.Equal("created", cache.Get(7));
        }

        [Fact]
        public async Task GetOrFetchAsync_retries_after_a_failed_fetch()
        {
            var cache = new EntityCache<string>(capacity: 8);
            var calls = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrFetchAsync(7, (_, __) =>
            {
                Interlocked.Increment(ref calls);
                throw new InvalidOperationException("offline");
            }).AsTask());
            var value = await cache.GetOrFetchAsync(7, (_, __) =>
            {
                Interlocked.Increment(ref calls);
                return new ValueTask<string>("created");
            });

            Assert.Equal("created", value);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void GetOrFetchAsync_hit_completes_synchronously()
        {
            var cache = new EntityCache<string>();
            cache.Set(1, "cached");

            var pending = cache.GetOrFetchAsync(1, (_, __) => new ValueTask<string>("new"));

            Assert.True(pending.IsCompletedSuccessfully);
            Assert.Equal("cached", pending.Result);
        }

        [Fact]
        public async Task ChannelSendQueue_keeps_per_channel_order_while_pruning()
        {
            var queue = new ChannelSendQueue(maxChannels: 16, idleLifetime: TimeSpan.Zero);
            var active = new int[4];
            var overlaps = 0;

            await Task.WhenAll(Enumerable.Range(0, 400).Select(i => Task.Run(() => queue.EnqueueAsync(i % 4, async () =>
            {
                if (Interlocked.Increment(ref active[i % 4]) != 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                await Task.Yield();
                Interlocked.Decrement(ref active[i % 4]);
            }))));

            Assert.Equal(0, overlaps);
        }

        [Fact]
        public void Set_evicts_lru_when_over_capacity()
        {
            var cache = new EntityCache<string>(capacity: 2);
            cache.Set(1, "a");
            cache.Set(2, "b");
            _ = cache.Get(1);
            cache.Set(3, "c");

            Assert.Equal("a", cache.Get(1));
            Assert.Null(cache.Get(2));
            Assert.Equal("c", cache.Get(3));
            Assert.Equal(2, cache.Count);
        }
    }
}
