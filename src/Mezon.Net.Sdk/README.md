# Mezon.Net.Sdk

High-level Mezon bot/application SDK for .NET — entities, typed message content, L1 cache, commands, and interactions.

## Packages

| Package | Role |
|---------|------|
| `Mezon.Net.Client` | Wire, protobuf decode, transport, typed `MessageContent` |
| `Mezon.Net.Sdk` | Entities, Command/Interaction core, L1 cache |
| `Mezon.Net.Sdk.Caching.Sqlite` | Optional L3 message history (WAL) |
| `Mezon.Net.Sdk.Caching.Redis` | Optional L2 DTO snapshots + invalidation |

## Quick start

```csharp
await using var client = new MezonClient(new MezonClientOptions(botId, token));

var commands = new CommandService("!")
    .AddCommand("ping", async ctx => await ctx.ReplyTextAsync("Pong!"));

client.UseCommands(commands);
await client.LoginAsync();
```

Send typed content (JS wire parity `{ "t": "hi" }`):

```csharp
await channel.SendTextAsync("hi");
await channel.SendAsync(MessageContent.CreateText("hi"));
```

## Entities and L1 cache

| Entity | Cache | Notes |
|--------|-------|-------|
| `Clan` | `client.Clans` | Seeded on connect; stubbed on install events |
| `Channel` | `client.Channels` | All `ChannelType` values (text, voice, thread, …) via `Type` |
| `Role` | `client.Roles` | Updated from `RoleChanged` / `RoleAssigned` payloads |
| `User` | `client.Users` | Sparse identity from messages / profiles |
| `Message` | `channel.Messages` | Per-channel LRU |

Clan-scoped views: `clan.Channels`, `clan.Roles` (`EntityCacheView`).

`Channel` exposes `ParentId`, `CategoryId`, `CategoryName`, `CreatorId`, `AppId`, `MeetingCode`, and `Type`. Call `clan.LoadChannelsAsync()` to hydrate L1 from the API when the app chooses to (not from event handlers).

## Architecture notes

- **Hot path**: ordered realtime dispatch, opt-in handler timeout, cached nested `ProtoListView`, LRU `EntityCache` with single-flight `GetOrFetchAsync`.
- **No API in events**: engine/SDK event subscribers must **not** call REST/socket API on the dispatch path. Cache listeners only mutate local L1 from the event payload (or create stubs). RT presence (`JoinClanChat` / `JoinChannelChat` / `LeaveChannelChat`) is allowed only for rare membership events and always fire-and-forget via background scheduling. Prefer stubs / `GetOrCreateChannelStub` over `GetChannelAsync` from event code.
- **Content**: `Mezon.Net.Client.MessageContent` (opt-in parse; raw wire string unchanged) — never hand-build `{"t":...}` in app code.
- **Cache layers**: L1 is always on. L2 (Redis) and L3 (Sqlite) are **app-owned sidecars** — see [docs/caching-l2-l3.md](../../docs/caching-l2-l3.md).
- **Commands / interactions**: first-class in .NET. Button/select events carry full wire payloads.

## Event → L1 side effects

| Sdk event | L1 / presence |
|-----------|----------------|
| `ClanJoined` | Stub clan + `JoinClanChat` (idempotent) |
| `ClanUserAdded` (bot) | Stub clan + `JoinClanChat`; upsert user |
| `UserChannelAdded` | Upsert channel from `channel_desc`; `JoinChannelChat` if bot |
| `UserChannelRemoved` (bot) | `LeaveChannelChat` + remove channel |
| `RoleChanged` | Upsert/remove role from payload (`status == 3` → delete) |
| `RoleAssigned` | Stub role + membership from assign/remove ids |
| `ChannelCreated` / `Updated` / `Deleted` | Channel cache mutate; thread activate may join chat |
| `ChannelMessage*` / reaction | Mutate message/user only if channel already cached |

## Multi-target

Shared paths target `netstandard2.1` + `net6.0`–`net10.0`. Prefer `#if NET8_0_OR_GREATER` for JSON source-gen / `TimeProvider` / channel optimizations. Benchmarks run on `net8.0+`.

## Benchmarks

```bash
dotnet run -c Release --project tests/Mezon.Net.Sdk.Benchmarks
dotnet run -c Release --project tests/Mezon.Net.Client.Benchmarks
```

## Success metrics (gates)

- `GetOrFetchAsync` stampede → 1 factory call
- `channel.SendTextAsync("hi")` wire JSON equals `{"t":"hi"}`
- Button clicks route by `button_id`
- Memory bounded via message LRU + send-queue idle prune
- L2 Redis invalidation does not require distributed locks for send
- Cache event handlers complete without awaiting REST API
