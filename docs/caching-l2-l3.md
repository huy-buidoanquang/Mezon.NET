# Caching layers (L1 / L2 / L3)

How process-local entity cache (L1) relates to optional Redis snapshots (L2) and SQLite message history (L3), and how to wire them into a Mezon bot **without** calling REST APIs from realtime event handlers.

## Layer model

| Layer | Package | What it stores | Lifetime | Wired by |
|-------|---------|----------------|----------|----------|
| **L1** | `Mezon.Net.Sdk` (`EntityCache`) | Live entities: `Clan`, `Channel`, `Role`, `User`, `Message` | Process | Automatic in `MezonClient` |
| **L2** | `Mezon.Net.Sdk.Caching.Redis` | **DTO snapshots only** (JSON), shared across nodes | TTL + invalidate | App (DI) |
| **L3** | `Mezon.Net.Sdk.Caching.Sqlite` | Durable **messages** (WAL) | Disk file per account/env | App (manual open) |

L2 and L3 are **sidecars**. `MezonClient` does not call Redis or Sqlite. Your app subscribes to Sdk events and persists in the background.

```text
Realtime envelope
  → Sdk L1 cache listeners (sync mutate / stub; no REST)
  → App event handlers (ScheduleBackground): L3 upsert / L2 Set|Invalidate
  → App business logic
```

**Hard rule:** never call `ListClanDescs`, `GetChannelDetail`, `ListRoles`, etc. from event handlers. High fan-out (especially messages) will stampede. Hydrate from API only on connect init, explicit app commands, or command/interaction background work.

Do not serialize live `Channel` / `Role` / socket / JWT into L2 — only plain DTOs.

## When to use which layer

| Scenario | Recommendation |
|----------|----------------|
| Single-process bot, small guilds | L1 only |
| Need message history across restarts | L1 + L3 |
| Multiple bot instances / pods sharing state | L1 + L2 (+ invalidation); L3 per instance if needed |
| Shared channel/clan metadata across nodes | L2 snapshots + pub/sub invalidation |

## L1 (built-in)

On `LoginAsync` / connect, Sdk seeds **clans** and `JoinClanChat` only — not all channels, roles, or messages (keeps `Ready` fast and avoids a fake-complete cache). Membership events stub clans/channels/roles and schedule RT joins. Rationale: [Sdk README](../src/Mezon.Net.Sdk/README.md#why-login-does-not-seed-every-entity). Event map: [events-and-cache.md](events-and-cache.md).

## L2 — Redis / Valkey

Contracts in Sdk: `IEntitySnapshotStore`, `CacheKey`, `CacheEntryOptions`, `ICacheInvalidationListener`.

### Setup

```csharp
services.AddMezonRedisEntitySnapshotStore(multiplexer, o =>
{
    o.KeyPrefix = "mezon:snapshot";
    o.InvalidationChannel = "mezon:snapshot:invalidate";
    o.DefaultAbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
});
services.AddMezonRedisSnapshotInvalidationListener(multiplexer, o =>
{
    o.InvalidationChannel = "mezon:snapshot:invalidate";
});
```

### CacheKey convention

`{env}:{accountId}:{entityType}:{id}`

Suggested `entityType` values: `clan`, `channel`, `user`, `role`.

### Glue pattern (app-owned)

```csharp
client.ChannelCreated += async evt =>
{
    // L1 already updated by Sdk. Persist DTO off the hot path.
    _ = PersistChannelSnapshotAsync(evt); // fire-and-forget / queue
};

listener.Invalidated += (_, key) =>
{
    // Drop L1 so the next read refreshes.
    if (key.EntityType == "channel" && long.TryParse(key.Id, out var id))
        client.Channels.Remove(id);
};
await listener.StartListeningAsync();
```

Redis failures are treated as miss/no-op — do not throw into the event dispatch path.

Package details: [Mezon.Net.Sdk.Caching.Redis README](../src/Mezon.Net.Sdk.Caching.Redis/README.md).

## L3 — Sqlite messages

`SqliteMessageStore` is message-only (not a general entity store). No DI extension yet — open explicitly.

### Setup

```csharp
var path = SqliteMessageStorePaths.ResolveDatabasePath(baseDir, accountId: botId.ToString(), env: "production");
await using var store = await SqliteMessageStore.OpenAsync(path);
```

### Map and persist (background)

| Event | Store API |
|-------|-----------|
| `ChannelMessageReceived` / update | `UpsertMessageAsync(MessageSnapshot, revision)` |
| `ChannelMessageRemoved` | `DeleteMessageAsync` |
| `MessageReactionReceived` | `ApplyReactionAsync` |

Map from `ChannelMessageResponse` → `MessageSnapshot` (`MessageId`, `ChannelId`, `ClanId`, `SenderId`, `Content`, `CreateTimeSeconds`, …). Use a monotonic `revision` so stale events cannot overwrite newer rows.

Enqueue from event handlers; do not block the websocket thread on disk I/O. Call `FlushAsync()` before shutdown if durability matters.

### Hydrate on Ready

On `client.Ready`, optionally load recent messages from L3 into `channel.Messages` for channels already in L1. Do **not** call REST message-history APIs unless the app explicitly needs them.

### Lifecycle

- One DB file per account + environment; do not share the file across pods.
- `PruneAsync(retention)` on a timer.
- Dispose store after `MezonClient` disconnect (or with host lifetime).

Package details: [Mezon.Net.Sdk.Caching.Sqlite README](../src/Mezon.Net.Sdk.Caching.Sqlite/README.md).

## Combined sequence

```text
LoginAsync
  → connect + SeedClanCache (L1 + JoinClanChat)
  → Ready
       → optional: hydrate Messages from L3 into L1
  → events
       → Sdk mutates L1
       → app: L3 upsert / L2 Set (background)
  → other node invalidates L2 key
       → listener removes L1 entry
```

## Multi-instance

- **Required:** Redis invalidation channel when more than one process holds L1.
- **Sqlite:** per-instance or shared storage with exclusive writer; prefer per-account path, not NFS share without locking.
- RT joins (`JoinClanChat`) should remain idempotent per process (Sdk tracks joined clans).

## Ops checklist

- Redis/Valkey connection string, key prefix, TTL
- Invalidation channel name consistent across pods
- Sqlite path permissions and disk growth (`PruneAsync`)
- Tests: unit-test L1 handlers without Redis/Sqlite; integration-test stores separately

## Out of scope (by design)

- `client.UseRedis()` / automatic L2/L3 inside `MezonClient`
- Sqlite DI extensions (open manually; may be added later)
