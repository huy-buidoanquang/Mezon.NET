# Mezon meeting SFU client (`Mezon.Net.Sdk.Sfu`)

`Mezon.Net.Sdk.Sfu` joins rooms of the Mezon meeting SFU (`mezon-sfu`) from .NET: bots, recorders and services that
publish Opus audio (and VP8 camera video) and receive the other participants' RTP. WebRTC is provided by
[SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) 10.0.16 (managed code, no native libraries).

> **Status: pre-1.0.** Everything listed under [Features](#features) works against a real `mezon-sfu` in both
> downlink modes (dynamic and fixed slots), on Windows and Linux. The API may still change before 1.0.

It is a port of the Rust SDK (`mezon-sfu-sdk-rust`), with the same protocol rules and the same tests. Targets:
`netstandard2.1`, `net6.0`, `net8.0`, `net9.0`, `net10.0`. It does not depend on `Mezon.Net.Sdk`.

## Usage

The SFU URL and the meet token come from the Mezon API (`GenerateMeetToken`). The token is a short-lived JWT for one
user in one room; use `TokenProvider.Create` (instead of `TokenProvider.Fixed`) to fetch a fresh one when the room
reconnects.

```csharp
using Mezon.Net.Sdk.Sfu;

await using var room = new Room(new RoomOptions());
await room.ConnectAsync(sfuUrl, TokenProvider.Fixed(meetToken));

// Sessions start muted on the server: enable the microphone, then write Opus frames (20 ms each here).
await room.LocalParticipant.SetMicrophoneEnabledAsync(true);
room.LocalParticipant.Microphone.WriteOpus(opusFrame, TimeSpan.FromMilliseconds(20), level: 30);

await foreach (var e in room.Events.ReadAllAsync())
{
    switch (e)
    {
        case TrackSubscribed subscribed:
            _ = Task.Run(async () =>
            {
                // Opus or VP8 RTP from subscribed.Participant; the loop ends when the track is unsubscribed.
                await foreach (var packet in subscribed.Track.ReadAllAsync())
                {
                    Consume(subscribed.Participant, packet);
                }
            });
            break;
        case MessageReceived message:
            Console.WriteLine($"{message.UserId}: {message.Message}");
            break;
        case Disconnected disconnected:
            Console.WriteLine($"left: {disconnected.Reason}");
            break;
    }
}
```

### Joining a Mezon voice channel from a Mezon.Net bot

```csharp
// client is a logged-in Mezon.Net.Sdk.MezonClient.
var tokens = TokenProvider.Create(async (request, cancellationToken) =>
    (await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId, null, "Music bot;"))).Token);
await room.ConnectAsync(sfuUrl, tokens);
```

The response also carries the SFU URL configured on the server (`GenerateMeetTokenResponse.Url`). Without
`Mezon.Net.Sdk`, `Mezon.Net.Sdk.Sfu.Core.MeetTokenMessages` encodes `GenerateMeetTokenRequest` and decodes the
response (protobuf or bare JWT) for the socket API.

### Lifetime, threading and events

- `ConnectAsync` returns once the session is admitted (room snapshot received) and media is connected. Cancelling
  its token abandons the attempt and leaves the room `Disconnected` (socket and PeerConnection closed).
- `DisposeAsync` (or `DisconnectAsync`) leaves the room: it returns once the socket and the PeerConnection (also one
  still being created) are closed and every background task stopped; a `ConnectAsync` still running ends at once,
  including while it waits for the token provider or the WebSocket handshake. Concurrent `DisposeAsync` calls all
  return once the first one finished. A room that is only dropped keeps its session until the process ends: always
  dispose it (`await using`). After `DisposeAsync`, `Events` completes and `ConnectAsync` throws
  `ObjectDisposedException`. `DisconnectAsync` allows connecting again. When the room ends (`Disconnected`), the
  microphone, camera and push-to-talk go back to off.
- `Events` is an unbounded `ChannelReader<RoomEvent>`: events arrive in order (`ConnectionStateChanged`,
  `Reconnecting`, `Reconnected`, `Disconnected`, `ParticipantConnected`/`Disconnected`/`Updated`,
  `TrackSubscribed`/`Unsubscribed`/`Muted`/`Unmuted`, `ActiveSpeakersChanged`, `MessageReceived`,
  `LocalMuteForced`, `LocalParticipantUpdated`, `DownlinkModeDetected`, `SignalError`). Applications must read it:
  no event is dropped, so a room whose events are never read keeps every event in memory until it is disposed.
  `ConnectAsync` can return before `ConnectionStateChanged(Connected)` is read.
- Every member is thread-safe. One lock guards the room state and is never held across an `await`; remote RTP goes
  from the WebRTC receive thread to the track's buffer without it.
- Errors are `SfuException` with a `Kind` (`NotConnected`, `State`, `Signaling`, `Timeout`, `Disconnected`,
  `Authentication`, `Negotiation`, `Protocol`, `Socket`, `WebRtc`) and a machine-readable `Code` (the server error
  code for `Signaling`). Caller cancellation throws `OperationCanceledException`.

## Options (`RoomOptions`)

| Option | Default | Meaning |
|--------|---------|---------|
| `Role` | `Speaker` | `Audience` listens and talks only while `SetPushToTalkAsync(true)`. |
| `ScreenCodec` | `Vp8` | Codec offered for the screen uplink (screen sharing is not published by this SDK). |
| `IceServers`, `IceServersPolicy` | none, `FromServer` | Extra STUN/TURN servers; `Merge` (default when set) or `Override` the ones announced in `joined`. |
| `Reconnect` | on, 8 attempts, 2 min | `Enabled`, `MaxAttempts`, `MaxDuration` per outage; `Delay(attempt)` (default equal jitter 0.5–1 s doubling to 4–8 s). The budget resets after 30 s connected. |
| `Timeouts` | 10/10/10/20/25/8 s | `Token` provider, `Dial`, `Join`, `Admission` (snapshot + media connected), `InboundIdle` (the server pings every 10 s), `Operation` (request acknowledgement). Negative or huge values mean no timeout. |
| `ActiveSpeakers` | 0.1, 2 samples, 1.5 s | Speaker detection in dynamic rooms (estimated from the Opus bitrate). |
| `ModerationTokens` | none | Provider of "mute"/"kick" moderation tokens for `MuteParticipantAsync`/`KickParticipantAsync`. |
| `BindAddress` | any | Local address of the WebRTC socket. Set `IPAddress.Loopback` when the SFU runs on 127.0.0.1. |
| `LoggerFactory` | none | Logs of the SDK (category `Mezon.Net.Sdk.Sfu`). Tokens are never logged, and `ToString()` of `TokenRequest`, `IceServer` and the meet-token responses prints `<redacted>` for tokens and credentials. SIPSorcery logs through `SIPSorcery.LogFactory.Set(...)`. |

## Media

- **Audio out:** `Microphone.WriteOpus(frame, duration, level)` sends one Opus frame per RTP packet (48 kHz clock).
  `level` is the RFC 6464 audio level, 0 (loudest) to 127 (silence); the SFU's voice activity detection uses it to
  pick active speakers in fixed-slot rooms. Without a level the frame counts as voice (as if the extension were
  absent). Frames written while the microphone is disabled, push-to-talk is released or no session is negotiated
  are dropped (`WriteOpus` returns false). `OggOpusReader` reads files packet by packet with their durations.
- **Video out:** `SetCameraEnabledAsync(true)` and `Camera.WriteVp8(frame, duration)` (speakers). Keyframe requests
  from receivers are not surfaced: send keyframes periodically.
- **Media in:** `RemoteTrack.ReadRtpAsync`/`ReadAllAsync` return the forwarded RTP (no decoding, `MimeType`
  `audio/opus` or `video/VP8`). A track buffers 512 packets and then drops the oldest (`Stats.PacketsDropped`).
  Tracks follow the publisher across slot reassignments in fixed-slot rooms; every change is an unsubscribe plus a
  subscribe. `OggOpusWriter` stores Opus packets as Ogg Opus.
- **Downlink modes:** dynamic rooms renegotiate one slot per publisher (msids name the publisher, mids are
  reused); fixed-slot rooms (`max_active_speakers`) reassign a pool of slots with `slot_assigned`/`slot_released`
  events. The mode is detected per session (`DownlinkMode`).
- **Audience:** `RoomOptions.Role = Role.Audience` listens; `SetPushToTalkAsync(true)` unmutes and publishes.

## SIPSorcery adaptation

`Peer/SipsorceryPeer.cs` is the only file that uses SIPSorcery; the rest of the SDK talks to an internal
PeerConnection interface that the integration tests replace with a fake. It works around these SIPSorcery 10.0.16
behaviours:

- Answers use `UDP/TLS/RTP/SAVP` unless `X_UseRtpFeedbackProfile` is set; mezon-sfu reads payload types only after
  `"SAVPF "` and rejects such an answer (`invalid_answer_sdp`).
- Streams that `setRemoteDescription` creates for m-lines without a local track have no RTP channel, SRTP context or
  event wiring, so their packets are dropped silently. Before each offer the adapter adds a receive-only local track
  for every offered m-line that has no stream yet.
- `createAnswer` turns inactive tracks back to their default direction (answering an inactive offer with
  `recvonly`) and adds `telephone-event` to audio. The answer is rewritten (`Sdp.NormalizeAnswer`): one section
  per offered m-line in order, mid 0 `sendonly`, mid 1 `sendonly` for speakers, mid 2 `inactive`, remote mids
  `recvonly` when offered `sendonly`, only offered payload types, and the BUNDLE group of the offered mids.
- Received RTP is reported per stream index of each kind; the adapter maps it to the offer's mids. Streams are
  matched by the SSRCs the offer announces, so video whose SSRC an offer does not announce yet (mezon-sfu writes
  `a=msid` without `a=ssrc` before it knows the publisher's SSRC) is delivered after the next offer.
- SIPSorcery writes every local header extension on every packet, so the audio-level extension is always sent; "no
  level" is sent as level 0 with the voice flag, which mezon-sfu's voice activity detection treats like a packet
  without the extension.
- There is no rollback; a failed offer is replaced by the next one (three failures in a row start a new session).
- Renegotiation can drop a few RTP packets: when an offer adds m-lines, the adapter adds tracks while SIPSorcery's
  receive thread looks up the streams of incoming packets without a lock, and a packet that arrives at that moment
  is dropped with an error log (`Exception RTCPeerConnection.OnRTPDataReceived`). The session continues.
- On Windows, a stack published on 127.0.0.1 needs `BindAddress = IPAddress.Loopback`.
- Its local ICE ufrag has only four letters; mezon-sfu routes by client ufrag across all rooms, so a collision with
  another session is possible on a busy server. The server rejects such an answer (`duplicate_ice_ufrag`) and the
  SDK reconnects at once with a new PeerConnection.

## Reconnect and tokens

- The room reconnects by itself after a dropped socket, an ICE failure or a server restart (close codes 1001, 1006,
  1011, 4001, 4002, 4007–4010, 4013, 4014), with jittered backoff (`RoomOptions.Reconnect`). Every reconnect is a new
  session (new peer id) that replays the microphone, camera, push-to-talk, video receiving and metadata.
- It stops for good on a kick (4006), the alone timeout (4011), a duplicate session (4012), a server close (1000), a
  full room, a room id the server rejects (error `invalid_room`, `JoinFailed`) or when the server is misconfigured
  (4003); `Disconnected` carries the reason. The error `room_creation_failed` ends the session at once and is
  retried like a dropped socket.
- `TokenProvider.Create((request, cancellationToken) => ...)` receives the reason (`Initial`, `Reconnect`,
  `Expiring`, `Rejected`) and the previous token (the rejected one after a 4004/4005). A failing or slow provider
  (`Timeouts.Token`, 10 s) counts as a failed attempt and is retried while reconnecting. `TokenProvider.Fixed`
  cannot replace a rejected token, so a 4005 ends the room with `AuthFailed`.
- One user can hold one SFU session at a time: joining again with the same user id, in any room, disconnects the
  older session (`DisconnectReason.DuplicateSession`).

## Features

|                                                | TypeScript | Go  | Rust | Kotlin | .NET | Python |
| ---------------------------------------------- | ---------- | --- | ---- | ------ | ---- | ------ |
| Dynamic and fixed-slot downlink                | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Microphone with RFC 6464 audio level           | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Camera                                         | ✓          | VP8 | VP8  | ✓      | VP8  | VP8    |
| Screen share                                   | browser    | –   | –    | –      | –    | –      |
| Push-to-talk (audience)                        | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Room messages, metadata, moderation            | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Video receiving on/off, keyframe requests      | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Active speakers                                | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |
| Reconnect with token refresh                   | ✓          | ✓   | ✓    | ✓      | ✓    | ✓      |

## Examples

```sh
MEZON_SFU_URL=wss://... MEZON_SFU_TOKEN=... dotnet run --project src/Mezon.Net.Sdk.Sfu.Example.Bot -- --ogg music.opus --loop --echo
MEZON_SFU_URL=wss://... MEZON_SFU_TOKEN=... dotnet run --project src/Mezon.Net.Sdk.Sfu.Example.Recorder -- --out recordings --duration 60
```

The bot plays an Ogg Opus file and echoes chat; the recorder joins as audience and writes each participant to its
own Ogg Opus file (`<userId>-p<peerId>-<n>.opus`; packets in arrival order, so silence and losses are not filled).
Pass `--bind-address 127.0.0.1` for an SFU on this machine's loopback. The token is read from the environment so it
stays out of the process list. Both examples are run against the real SFU by `ExampleTests`.

## Compatibility and known server issues

Tested against mezon-sfu `0520143` (`tools/SfuE2e/stack/sfu.lock`) in dynamic and fixed-slot rooms, on Windows and in
Linux containers, and with the other SDKs (`mezon-sfu-sdk-js` `test/e2e-node/interop.e2e.test.ts` records a JS tone with
this SDK's recorder example and plays it back with its bot example).

- Servers built before mezon-sfu `0520143` (`mezonai/mezon-sfu#480`) relay participant metadata and room messages without escaping control
  characters other than CR, LF and TAB, and without validating UTF-8. The SDK drops raw control characters from
  server frames and parses them again, but a frame with invalid UTF-8 fails the socket read (.NET's
  WebSocket validates text frames) and the session reconnects. Invalid UTF-8 in a participant's metadata is in
  every snapshot, so while that participant stays in the room every rejoin fails the same way: the room ends with
  `ReconnectExhausted`, or the first connect with `JoinFailed`.
- Servers built before mezon-sfu `6a05e2b` also drop `peer_left` in fixed-slot rooms and for a peer that leaves within
  about three seconds (`mezonai/mezon-sfu#476`), and accept kick and mute moderation tokens as join tokens
  (`mezonai/mezon-sfu#477`).
- Since mezon-sfu `0520143` (`mezonai/mezon-sfu#482`), an audience member's push-to-talk also reaches the other
  audience members; older servers forward it to speakers only.
- The server debounces renegotiation for 2–3 s: a participant who joins later receives existing publishers only
  after that.

## Other SDKs

| SDK | Repository / package | WebRTC stack | Media API | Runs on |
| --- | --- | --- | --- | --- |
| TypeScript | `mezonai/mezon-sfu-sdk-js`, npm `mezon-sfu-sdk` | browser WebRTC; `@roamhq/wrtc` (libwebrtc) in Node | `MediaStreamTrack`; PCM source/sink (Node) | browsers, Node 20+ |
| Go | `mezonai/mezon-sfu-sdk-go` | pion/webrtc v4 | Opus and VP8 RTP | servers, bots |
| Rust | `mezonai/mezon-sfu-sdk-rust` (`mezon-sfu-core`, `mezon-sfu`) | webrtc-rs 0.21 | Opus and VP8 RTP | servers, bots |
| Kotlin | `mezonai/mezon-sfu-sdk-kotlin` | libwebrtc: webrtc-java (JVM), webrtc-sdk/android (Android) | PCM, I420 video | JVM 17+, Android 7+ (API 24) |
| **.NET** | **`huy-buidoanquang/Mezon.NET`, NuGet `Mezon.Net.Sdk.Sfu`** | **SIPSorcery 10** | **Opus and VP8 RTP** | **.NET Standard 2.1, .NET 6–10** |
| Python | `mezonai/mezon-sfu-sdk-python`, PyPI `mezon-sfu-sdk` | aiortc 1.15 | PCM, I420 video | Python 3.11+ |

## Development

```sh
dotnet build Mezon.Net.sln -c Release
dotnet test tests/Mezon.Net.Sdk.Sfu.Tests/Mezon.Net.Sdk.Sfu.Tests.csproj -c Release
```

The test project (`net10.0`) has five levels:

- `ProtocolTests`, `ModelTests`, `OggOpusTests`: the pure core (codec key order and escaping, tolerant decoding,
  close/error tables, JWT claims, metadata, meet-token protobuf, SDP layout, membership, slot map, dynamic
  occupancy, mute sync, reconnect policy, speaker detector, Ogg Opus). `Tables_match_the_protocol_spec` compares the
  tables with `../mezon-sfu/docs/protocol/*.json` (or `MEZON_SFU_PROTOCOL_DIR`) and the meet-token messages are
  checked against `../mezon-protocol/api/api.proto` (or `MEZON_PROTOCOL_DIR`); both skip when absent. The vendored
  `Mezon.Net.Core` copy of `api.proto` and its generated types are always checked.
- `OracleTests`: SIPSorcery answers to synthetic mezon-sfu offers must pass a port of the server's
  `validate_answer_mline_shape` and keep the uplink directions.
- `IntegrationTests`: the real `Room` against a scripted SFU on a real WebSocket plus a fake PeerConnection,
  including the regression tests of every pitfall found in the other SDKs' reviews.
- `E2eTests`, `ExampleTests`: the real SFU in Docker; they skip when their stack is not running.

### Real-SFU stacks (Docker)

`tools/SfuE2e` starts nats and mezon-sfu with the same image, templates and Dockerfile as the other SDKs
(`tools/SfuE2e/stack/` holds copies: keep them identical in all six; `stack/sfu.lock` pins mezon-sfu `develop`
0520143). `SFU_IMAGE` runs an existing image and is never rebuilt; without it, `up` builds `../mezon-sfu` into the
shared tag `mezon-sfu:e2e` unless `--no-build`:

```sh
SFU_IMAGE=mezon-sfu:develop-0520143 dotnet run --project tools/SfuE2e -- up --mode dynamic --no-build
dotnet run --project tools/SfuE2e -- up --mode fixed2 [--net host|bridge] [--no-build]
dotnet run --project tools/SfuE2e -- test          # the test project in mcr.microsoft.com/dotnet/sdk:10.0 on every bridge stack
dotnet run --project tools/SfuE2e -- down [--mode <mode>] [--keep]
```

- Modes: `dynamic`, `fixed2` (`max_active_speakers = 2`) and `alone` (alone timeout 8 s). Every SDK has its own
  ports, subnets and compose projects, so all stacks can run together; never stop another SDK's stacks:

  | SDK | Media / signaling / NATS ports | Subnets | Compose projects | Command |
  | --- | --- | --- | --- | --- |
  | TypeScript | 7000-7002 / 8000-8002 / 4222-4224 | 172.28.0-2 | `sfu-e2e-<mode>` | `node scripts/e2e-up.mjs` |
  | Go | 7010-7012 / 8010-8012 / 4232-4234 | 172.28.10-12 | `sfu-e2e-go-<mode>` | `go run ./e2e/stack` |
  | Rust | 7020-7022 / 8020-8022 / 4242-4244 | 172.28.20-22 | `sfu-e2e-rust-<mode>` | `cargo xtask e2e` |
  | Kotlin | 7030-7032 / 8030-8032 / 4252-4254 | 172.28.30-32 | `sfu-e2e-kotlin-<mode>` | `./gradlew :harness:run` |
  | .NET | 7040-7042 / 8040-8042 / 4262-4264 | 172.28.40-42 | `sfu-e2e-dotnet-<mode>` | `dotnet run --project tools/SfuE2e` |
  | Python | 7050-7052 / 8050-8052 / 4272-4274 | 172.28.50-52 | `sfu-e2e-python-<mode>` | `python -m tools.e2e` |

- `host` (default on Windows/macOS) publishes on 127.0.0.1 for tests on this machine; `bridge` (default on Linux) is
  for containers. `test` attaches the container to every running bridge stack and runs with
  `--artifacts-path /artifacts` (Docker volumes `sdk-dotnet-nuget`, `sdk-dotnet-artifacts`).
- The JWT secret is generated into `tools/SfuE2e/stack/.secrets/jwt` and connection details into
  `stack/.runtime/<mode>.json` (both git-ignored, never printed); tests sign tokens themselves.
  `SFU_E2E_NET=host|bridge|none` restricts which stacks tests use. `down` saves scrubbed logs to
  `tools/SfuE2e/artifacts/`.
