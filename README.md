# Mezon.Net Architecture

Four-layer .NET SDK for the Mezon platform:

```
Mezon.Net.Sdk          → Bot/App facade (MezonBotClient, DI)
Mezon.Net.Client       → Socket engine (cid map, Envelope, api-over-socket, events)
Mezon.Net.Transport    → TCP (primary) + WebSocket (browser)
Mezon.Net.Core         → Protobuf, contracts, options, protocol maps
```

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
| `Mezon.Net.Sdk` | `MezonBotClient` + `AddMezonBotClient()` DI |

## Build notes

- Protobuf codegen uses `Grpc.Tools`; `Directory.Build.props` resolves `protoc` from the user NuGet cache on Windows.
- Set `NUGET_PACKAGES` if building in isolated environments.
