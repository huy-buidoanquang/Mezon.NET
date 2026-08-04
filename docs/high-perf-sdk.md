# High-performance Mezon.Net SDK

Implementation notes for the hybrid cache + typed content + commands/interactions redesign.

## Consistency model

```text
Server/event stream authoritative
→ normalize event + revision
→ apply L1 in-place (no REST from event handlers)
→ persist L3 (messages, optional SQLite) — app-owned background
→ update/invalidate L2 (optional Redis) — app-owned background
→ raise app events after local apply
```

See [`caching-l2-l3.md`](caching-l2-l3.md) and [`events-and-cache.md`](events-and-cache.md).

## Phase checklist

| Phase | Status | Gate |
|-------|--------|------|
| P0 Hot-path | Done | Timeout opt-in; ordered dispatch; ProtoListView cache; EntityCache LRU + single-flight; send-queue prune; Ready AsyncEvent; dispose disconnects |
| P1 MessageContent | Done | `SendTextAsync("hi")` → `{"t":"hi"}`; Unicode offsets; metadata JSON fallback; Add/RemoveReaction; builders |
| P2a L1 | Done | Identity stability; update/remove/reaction coherence; clan-scoped channels; per-client SessionManager |
| P3.0 Wire interactions | Done | Button/select payloads on Client + Sdk |
| P3.1 Commands | Done | CommandService + Sdk.Example migration |
| P2b SQLite | Done | Optional WAL store, revisions, retention |
| P3.2–3.3 Interactions | Done | InteractionRouter + collectors |
| P2c Redis | Done | Optional L2 snapshots + invalidation |

## Anti-patterns avoided

- Global process-wide HTTP serializer / shared delay
- Infinite polling throttle
- Sync SQLite on WS thread
- Mutating host `.gitignore`
- Unbounded caches with fake FIFO “LRU”
- Distributing live entities / sockets / JWT via Redis

## Running verification

```bash
dotnet test tests/Mezon.Net.Sdk.Tests -f net8.0
dotnet test tests/Mezon.Net.Client.Tests -f net8.0 --filter "FullyQualifiedName~EventDispatch|FullyQualifiedName~SessionManager|FullyQualifiedName~ChannelMessage"
dotnet test tests/Mezon.Net.Sdk.Caching.Sqlite.Tests -f net8.0
dotnet test tests/Mezon.Net.Sdk.Caching.Redis.Tests -f net8.0
dotnet run -c Release --project tests/Mezon.Net.Sdk.Benchmarks -- --filter *EntityCache*
```
