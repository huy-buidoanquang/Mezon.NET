# Mezon.Net.Sdk.Caching.Sqlite

Optional SQLite-backed persistent message cache for Mezon.Net applications.

## Features

- One database file per account and environment
- WAL journal mode with schema migrations
- Revision-guarded upsert, delete, and reaction updates
- Background batched writes (websocket handlers enqueue only)
- Retention pruning and explicit clear/dispose

## Install

```bash
dotnet add package Mezon.Net.Sdk.Caching.Sqlite
```

## Usage

```csharp
using Mezon.Net.Sdk.Caching.Sqlite;

var path = SqliteMessageStorePaths.ResolveDatabasePath(
    baseDirectory: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyBot", "cache"),
    accountId: "bot-123",
    environment: "production");

await using var store = await SqliteMessageStore.OpenAsync(path);

// From websocket/API events — returns immediately; disk I/O runs off the hot path.
await store.UpsertMessageAsync(new MessageSnapshot
{
    MessageId = message.MessageId,
    ChannelId = message.ChannelId,
    ClanId = message.ClanId,
    SenderId = message.SenderId,
    Content = message.Content,
    CreateTimeSeconds = message.CreateTimeSeconds,
}, revision: messageEvent.Revision);

await store.FlushAsync(); // optional: before shutdown or when tests need durability

// Hydrate on startup
var cached = await store.TryGetMessageAsync(channelId, messageId);

// Housekeeping
await store.PruneAsync(TimeSpan.FromDays(30));
```

Pick a local app-data directory for `baseDirectory`. This package does not modify `.gitignore` and does not assume shared network storage.

## Integration notes

- Map `Mezon.Net.Models.ChannelMessageResponse` (or your own DTO) into `MessageSnapshot` at the SDK boundary.
- Use monotonically increasing `revision` values (event sequence, wall clock, or hybrid) so stale events cannot overwrite newer rows.
- Call `FlushAsync()` before process exit if you need guaranteed durability.
