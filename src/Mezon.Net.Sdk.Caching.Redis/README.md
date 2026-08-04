# Mezon.Net.Sdk.Caching.Redis

Optional Redis/Valkey **L2 snapshot cache** for `Mezon.Net.Sdk`. Store **DTO snapshots only** — never live entities, sockets, JWT/session state, send locks, MMN keys, or `SemaphoreSlim`.

**Integration guide (L1 + L2 + L3):** [docs/caching-l2-l3.md](../../docs/caching-l2-l3.md)

Shared abstractions live in `Mezon.Net.Sdk`:

- `IEntitySnapshotStore`
- `CacheKey` (`{env}:{accountId}:{entityType}:{id}`)
- `CacheEntryOptions`
- `ICacheInvalidationNotifier` / `ICacheInvalidationListener`

## Install

```bash
dotnet add package Mezon.Net.Sdk.Caching.Redis
```

Requires a `StackExchange.Redis` `IConnectionMultiplexer` (Redis or Valkey).

## Usage

```csharp
using Mezon.Net.Sdk.Caching;
using Mezon.Net.Sdk.Caching.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

var redis = ConnectionMultiplexer.Connect("localhost:6379");

var services = new ServiceCollection();
services.AddLogging();
services.AddMezonRedisEntitySnapshotStore(redis, options =>
{
    options.KeyPrefix = "mezon:snapshot";
    options.InvalidationChannel = "mezon:snapshot:invalidate";
    options.DefaultAbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
});

// Optional: listen for cross-node invalidations and drop local L1 entries.
services.AddMezonRedisSnapshotInvalidationListener(redis, options =>
{
    options.InvalidationChannel = "mezon:snapshot:invalidate";
});

await using var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<IEntitySnapshotStore>();
var listener = provider.GetService<ICacheInvalidationListener>();
listener!.Invalidated += (_, key) => Console.WriteLine($"Invalidate L1 for {key}");
await listener.StartListeningAsync();

var key = new CacheKey("prod", accountId: 42, entityType: "channel", id: "9001");

// Read-through pattern: L2 miss falls back to API, then populate Redis.
// Call API only from explicit app paths — never from realtime event handlers.
var cached = await store.GetAsync<ChannelSnapshotDto>(key, cancellationToken);
if (cached is null)
{
    var dto = await FetchChannelFromApiAsync(9001, cancellationToken);
    await store.SetAsync(
        key,
        dto,
        new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            Revision = dto.Revision // CAS via Lua; stale writes are skipped
        },
        cancellationToken);
}

// Explicit invalidation publishes to InvalidationChannel when configured.
await store.InvalidateAsync(key, cancellationToken);
```

## Behavior

| Topic | Behavior |
|-------|----------|
| Key format | `{KeyPrefix}:{env}:{accountId}:{entityType}:{id}` (+ `:rev` for CAS) |
| Redis failure | Logged and treated as cache miss / no-op (never thrown to callers) |
| Revision / CAS | Optional `CacheEntryOptions.Revision` uses a Lua compare-and-set on `:rev` |
| Invalidation | `InvalidateAsync` publishes when `InvalidationChannel` is set; listener drops L1 |

Wire L2 updates from Sdk events in the **background** after L1 apply. Do not block the websocket dispatch path.
