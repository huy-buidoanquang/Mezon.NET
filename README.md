# Mezon.Net Architecture

Four-layer .NET SDK for the Mezon platform:

```
Mezon.Net.Sdk          → Bot/App facade (MezonClient, entities, DI, MMN/SSE)
Mezon.Net.Mmn          → MMN/ZK HTTP clients
Mezon.Net.Client       → Socket engine (cid map, Envelope, api-over-socket, events)
Mezon.Net.Transport    → TCP (primary) + WebSocket (browser)
Mezon.Net.Core         → Protobuf, contracts, options, protocol maps
```

## Sdk quickstart

```csharp
await using var client = new Mezon.Net.Sdk.MezonClient(new MezonClientOptions(botId, token));
client.OnChannelMessage(msg => { ... });
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
