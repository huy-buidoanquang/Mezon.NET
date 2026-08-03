# Mezon.Net Architecture

Four-layer .NET SDK for the Mezon platform:

```
Mezon.Net.Sdk          → Bot/App facade (MezonClient, entities, DI, MMN/SSE)
Mezon.Net.Mmn          → MMN gRPC node client + ZK prove HTTP client
Mezon.Net.Client       → Socket engine (cid map, Envelope, api-over-socket, events)
Mezon.Net.Transport    → TCP (primary) + WebSocket (browser)
Mezon.Net.Core         → Protobuf, contracts, options, protocol maps
```

## Two audiences, two packages

The library is designed around two distinct developer experiences:

| Audience | Package to reference | Entry point | Surface |
|----------|----------------------|-------------|---------|
| Bot / Channel-app dev | `Mezon.Net.Sdk` only | `Mezon.Net.Sdk.MezonClient` | Curated, high-level: `LoginAsync`, entities (`Clan`/`TextChannel`/`Message`/`User`), past-tense events (`ChannelMessageReceived`), builders, MMN, quick menu |
| UI / Client dev | `Mezon.Net.Client` (+ `Transport`/`Core` if needed) | `Mezon.Net.Client.MezonClient` | Full engine: socket lifecycle, typed facades (`IMezonClientApi`, `IMezonClientRealtime`), `Mezon.Net.Models` params/data views, all events |

- `Mezon.Net.Sdk` references `Mezon.Net.Client` transitively (`ProjectReference`), so bot devs add **only** `Mezon.Net.Sdk`.
- On net6.0+, `Mezon.Net.Sdk` also depends on the published `Mezon.Net.Mmn` package (MMN gRPC + ZK). On `netstandard2.1`, MMN APIs are stubbed out.
- The SDK does not expose the underlying engine: `Engine`/`ApiClient` and internal protobuf plumbing are `internal`, so bot code stays on the ergonomic surface. All messaging (including ephemeral) is composed in the Client layer (`MessageSendHelper`), never by hand-building protobuf in the SDK.

### Protobuf boundary (`Mezon.Net.Models`)

Public API types live in `Mezon.Net.Models` (generated under `Mezon.Net.Client/Models/`):

| Direction | Type pattern | Example |
|-----------|--------------|---------|
| Request | `*Params` readonly struct | `ListClanDescParams`, `SendChannelMessageParams` |
| Response | `*Response` view struct (wraps proto, zero extra alloc) | `ChannelMessageResponse`, `ClanDescListResponse`, `AddFavoriteChannelResponse` |
| Events | `*EventData` (implicit → `*Response`) | `ChannelMessageEventData` |

Facades are generated on base classes (not on `MezonClient` directly):

- `BaseMezonClient` — REST/auth bootstrap (~7 methods)
- `BaseSocketClient` — socket API (~210 methods) + **realtime envelope** (21 `*RtAsync` methods) + payload events
- `MezonClient` — connect, heartbeat, event dispatch only

Two socket send paths (parity with mezon-js):

| Path | Interface | Example |
|------|-----------|---------|
| Socket API (`/mezon.api.Mezon/...`) | `IMezonClientApi` | `SendChannelMessageAsync`, `UpdateChannelMessageAsync` |
| Realtime envelope (direct `Envelope` oneof) | `IMezonClientRealtime` | `SendChatMessageRtAsync`, `JoinChannelChatRtAsync`, `LeaveChannelChatRtAsync` |

Realtime methods use the `RtAsync` suffix; mezon-js `write*` maps to `Send*RtAsync`.

`IMezonApiClient` / `IMezonSocketClient` / `ApiClient` are **internal**. Regenerate models/facades with `python tools/generate_protobuf_boundary.py`; run `python tools/compare_mezon_js_parity.py` to verify mezon-js parity.

### Namespace convention

The Client family all lives under `Mezon.Net.*`:

| Namespace | Contains |
|-----------|----------|
| `Mezon.Net.Client` | `MezonClient` (engine), socket clients, events |
| `Mezon.Net.Models` | Public `*Params` / `*Response` / `*EventData` (generated) |
| `Mezon.Net.Abstractions` | `IMezonClientApi`, `IMezonClientRealtime`, `ISession`, provider interfaces |
| `Mezon.Net.DependencyInjection` | DI extensions for the engine |
| `Mezon.Net.Client.Messaging` / `.Managers` | `MessageSendHelper`, `DmChannelManager` |

Legacy JSON DTOs under `Mezon.Net.Client/Api/` remain for a few auth helpers (`EmailAuthenticationRequest`, …) but are not the primary API surface.

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

See [`src/Mezon.Net.Sdk.Example`](src/Mezon.Net.Sdk.Example) for a sample bot host (env/CLI config, commands, graceful shutdown).

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
