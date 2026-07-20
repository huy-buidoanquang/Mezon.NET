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

## Architecture notes

- **Hot path**: ordered realtime dispatch, opt-in handler timeout, cached nested `ProtoListView`, LRU `EntityCache` with single-flight fetch.
- **Content**: `Mezon.Net.Client.MessageContent` (opt-in parse; raw wire string unchanged) — never hand-build `{"t":...}` in app code.
- **Cache**: process-local L1 identity map (mutate in-place). Redis/SQLite hold DTO snapshots / history only — not live entities or sessions.
- **Commands / interactions**: first-class in .NET (not present in the TypeScript SDK). Button/select events carry full wire payloads.

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
