# Mezon.Net Architecture

Four-layer .NET SDK for the Mezon platform:

```
Mezon.Net.Sdk          → Bot/App facade (MezonClient, entities, DI, MMN/SSE)
Mezon.Net.Mmn          → MMN/ZK HTTP clients
Mezon.Net.Client       → Socket engine (cid map, Envelope, api-over-socket, events)
Mezon.Net.Transport    → TCP (primary) + WebSocket (browser)
Mezon.Net.Core         → Protobuf, contracts, options, protocol maps
```

## Two audiences, two packages

The library is designed around two distinct developer experiences:

| Audience | Package to reference | Entry point | Surface |
|----------|----------------------|-------------|---------|
| Bot / Channel-app dev | `Mezon.Net.Sdk` only | `Mezon.Net.Sdk.MezonClient` | Curated, high-level: `LoginAsync`, entities (`Clan`/`TextChannel`/`Message`/`User`), past-tense events (`ChannelMessageReceived`), builders, MMN, quick menu |
| UI / Client dev | `Mezon.Net.Client` (+ `Transport`/`Core` if needed) | `Mezon.Net.Client.MezonClient` | Full engine: raw socket, `IMezonApiClient` (100+ methods), realtime `Envelope`, all events |

- `Mezon.Net.Sdk` references `Mezon.Net.Client` transitively (`ProjectReference`), so bot devs add **only** `Mezon.Net.Sdk`.
- The SDK does not expose the underlying engine: `Engine`/`Api` and internal plumbing are `internal`, so bot code stays on the ergonomic surface. All messaging (including ephemeral) is composed in the Client layer (`MessageSendHelper`), never by hand-building protobuf in the SDK.

### Namespace convention

The Client family all lives under `Mezon.Net.*`:

| Namespace | Contains |
|-----------|----------|
| `Mezon.Net.Client` | `MezonClient` (engine), socket clients, events |
| `Mezon.Net.Api` | Requests/responses, session, `MezonApiClient` |
| `Mezon.Net.Abstractions` | `IMezonApiClient`, `ISession`, provider interfaces |
| `Mezon.Net.DependencyInjection` | DI extensions for the engine |
| `Mezon.Net.Client.Messaging` / `.Managers` | `MessageSendHelper`, `DmChannelManager` |

The bot-facing surface lives under `Mezon.Net.Sdk` (and `Mezon.Net.Sdk.Entities`, `Mezon.Net.Sdk.Builders`, ...).

### Event naming

| Layer | Pattern | Example |
|-------|---------|---------|
| Sdk (`Mezon.Net.Sdk`) | past tense, no `Event` suffix | `ChannelMessageReceived` |
| Client engine (`Mezon.Net.Client`) | past tense + `Event` suffix | `ChannelMessageReceivedEvent` |
| Lifecycle | past tense verb | `Connected`, `Disconnected`, `Reconnecting` |

Protobuf envelope field/type names (`ChannelArchiveEvent`, `TopicInMessageEvent`, …) are unchanged; only the C# event surface follows the table above.

## Sdk quickstart

```csharp
await using var client = new Mezon.Net.Sdk.MezonClient(new MezonClientOptions(botId, token));
client.ChannelMessageReceived += msg => { ... };
await client.LoginAsync();
var channel = await client.GetChannelAsync(channelId);
await channel.SendAsync("Hello");
```

See `src/Mezon.Net.Sdk.Example` for a minimal bot host.

## Socket protocol

- **Outbound:** protobuf `realtime.proto::Envelope` with `cid` 1–65535 (wrap); `cid=0` = server push.
- **Inbound:** leading `0xFF` = raw API response (`cid` u16 BE + `code` u32 BE with FIN `0xff`, chunked); otherwise abridged `Envelope`.
- **API-over-socket:** `Envelope.api_request_event { api_index, api_name, body }` with protobuf body (see `ApiNameIndexMap`).
- **Auth bootstrap:** minimal HTTP REST for initial session; refresh via socket `SessionRefresh`.

## Projects

| Project | Role |
|---------|------|
| `Mezon.Net.Core` | `IMezonNetworkTransporter`, `MezonFrame`, protobuf (`Internal.Api`, `Internal.Realtime`) |
| `Mezon.Net.Transport` | `MezonNetworkTcpTransporter`, `MezonNetworkWebSocketTransporter` |
| `Mezon.Net.Client` | Unified `MezonClient` (merged former Api+Client) |
| `Mezon.Net.Sdk` | `MezonClient`, entities (`Clan`, `TextChannel`, `Message`, `User`), `AddMezonClient()` DI |

## Build notes

- Protobuf codegen uses `Grpc.Tools`; `Directory.Build.props` resolves `protoc` from the user NuGet cache on Windows.
- Set `NUGET_PACKAGES` if building in isolated environments.
