# Changelog

## 1.11.0

### Deprecated

- These `Mezon.Net.Sdk` methods are `[Obsolete]`, because mezon-proto-server now answers bot sessions with 403 for
  their APIs and the call throws `MezonApiException` (`RawCode` 403):
  - `MezonClient.ListChannelMessagesAsync`, `ListClanUsersAsync`, `ListRoleUsersAsync` and `Clan.ListRoleUsersAsync`
  - `MezonClient.ListQuickMenuAccessAsync`
  - `MezonClient.UploadAttachmentFileAsync`, `MultipartUploadAttachmentFileStartAsync` and
    `MultipartUploadAttachmentFileFinishAsync`

### Server changes since 1.10.0

- mezon-proto-server (`a3aa707`, on `main` since mezonai/mezon-proto-server#511) rejects every socket API a bot calls
  unless it is in `is_bot_allowed_api`: ListChannelDescs, ListChannelDetail, ListClanDescs, CreateChannelDesc,
  ListChannelVoiceUsers, ListRoles, CreateRole, UpdateRole, GetRoleOfUserInTheClan, ListFriends, AddFriends,
  Add/Update/DeleteQuickMenuAccess, GenerateMeetToken, Send/Update/DeleteChannelMessage, CreatePoll, ClosePoll,
  GetPoll, GenerateCDNSignature and GetStreamPlayback. Realtime sends (`*RtAsync`) are not affected.

## 1.9.1

### Added

- `Mezon.Net.Sdk.MezonClient.GenerateCDNSignatureAsync` (socket API 212), forwarding the engine's call. It returns
  `Signature`, the `<ts>-<mac>` query that signs every file under the channel's folder (`/<channel id as %016x>/…`)
  on Mezon's CDN for 3 hours. mezon-proto-server lets bots call it, and answers 403 when the caller is neither in the
  channel nor, for a public channel, in its clan. Treat the signed URL as a bearer token: never log it.

## 1.9.0

### Added

- `Mezon.Net.Sdk.Sfu` joins mezon-sfu's decoupled rooms (`media_pool_mode = decoupled`, mezon-sfu `19f885a` and later).
  1.8.0 could not: those rooms refuse a join without `media_pool_version: 1`.
  - Every join declares `media_pool_version: 1`; legacy rooms ignore it.
  - `DownlinkMode.Decoupled` follows the room's audio, camera and screen pools (`slot_assigned`/`slot_released` with
    `kind`, and `media_slots` in snapshots and peer events). Active speakers come from the audio pool.
  - `docs/sfu.md` lists the mezon-sfu `19f885a` issues that the SDK and its e2e stack work around.

### Fixed

- Socket requests that mezon-proto-server rejects now throw `MezonApiException`. proto-server answers a rejected
  socket API call or realtime send with an `Error` envelope that carries the request's cid; 1.8.0 and earlier parsed
  that envelope as the response, so the caller got an empty response instead of an exception. `RawCode` is the code
  from the envelope (for example 403 when a bot calls an API that bots may not use) and `Detail` its message.
  - This covers every socket API and the realtime sends that wait for an ack (`SendChatMessageRtAsync`,
    `UpdateChatMessageRtAsync`, `RemoveChatMessageRtAsync`, `SendEphemeralMessageRtAsync`), which now throw where
    they returned an empty ack.

### Server changes since 1.8.0

- mezon-proto-server no longer sends Envelope 106 (mezonai/mezon-proto-server#507), so `StreamingViewerCountChanged`
  does not fire. Count viewers from `ListStreamingChannelUsers` and the Joined/Leaved events instead.
- A viewer no longer expires 90 s after its last `watching: true`; it stays listed until it sends `watching: false` or
  the stream ends. Joined comes for every viewer, not only for the first 100.

## 1.8.0

### Added

- `GetStreamPlaybackAsync` (socket API 213), on the engine and on `Mezon.Net.Sdk.MezonClient`, for stream channels
  served by mezon-streaming-server. `publish: true` returns a publish token and the WHIP URL; `publish: false` returns a
  view token and the HLS URL. Socket only: the REST client throws `NotSupportedException`.
  - `ExpiresAt` is in Unix seconds. The node checks the token only when a session starts, so a running session
    outlives it; fetch a new grant for every (re)connect. Publish tokens last 10 minutes, view tokens 6 hours.
  - `WhepUrl` is empty: mezon-proto-server hands viewers only `HlsUrl`.
  - Never log `Token`, `HlsUrl` or a WHIP/WHEP resource URL.
- `LinkCDNFileAsync` (socket API 214) on the engine.
- `SendStreamingWatchRtAsync` (Envelope 104, mezon-js `writeStreamingWatch`): tell mezon-proto-server that the user
  starts or stops watching a stream channel. Clients renew `watching: true` every 30 s; a viewer that stops renewing is
  dropped after 90 s.
- Streaming events with payloads; the parameterless `StreamingJoined`/`StreamingLeaved` events keep firing:
  - `StreamingJoinedDataEvent` / Sdk `StreamingJoinedData`, with `IsPublisher`. It comes for the publisher and for at
    most 100 listed viewers per channel.
  - `StreamingLeavedDataEvent` / Sdk `StreamingLeavedData`.
  - `StreamingViewerCountChangedEvent` / Sdk `StreamingViewerCountChanged` (Envelope 106): distinct users watching,
    without the publisher. It is sent to the clan at most once per 10 s per channel and only when the count changes.
- `MezonApiException.RawCode`: the code exactly as the server sent it. mezon-proto-server answers some socket APIs
  with HTTP-style codes (400, 403, 500, 503), which `StatusCode` reports as `Unknown`.
- `Mezon.Net.Sdk.Sfu`, a client for the Mezon meeting SFU (`docs/sfu.md`), is published for the first time.

### Protocol

- Synced with mezon-protocol v2.0.97: `GetStreamPlaybackRequest`/`Response`, `LinkCDNFileRequest`, `StreamingWatch`
  (Envelope 104), `StreamingViewerCountEvent` (Envelope 106), `is_publisher` on `StreamingJoinedEvent` and
  `StreamingChannelUser`, `ApiRequestEvent.region` and `SdTopic.unread_count`. The new breakout-room and
  everyone-channel override messages have no API in mezon-js and none here.
- Envelope 101 is mezon-proto-server's `MetricMessageEvent` session probe and 105 the SFU breakout room event; both are
  ignored, as in mezon-js. mezon-protocol v2.0.87 to v2.0.96 had the viewer count on 101; nothing released read it.

### Upgrading

- **Mezube:** get WHIP grants with `GetStreamPlaybackAsync(new GetStreamPlaybackParams(channelId, publish: true))`.
  Treat `RawCode` 3, 5, 7, 400, 403 and 404 as "cannot stream in this channel" and anything else as temporarily
  unavailable. Bot sessions need mezon-proto-server with mezonai/mezon-proto-server#505; older servers answer 403.
- **Monze:** no change.
- Servers without the streaming configuration answer 213 with 503, and a channel missing from the server cache with
  500.

## 1.7.0

### Security

- **Button clicks are now `InteractionActorTrust.ClientSupplied`.** mezon-api `MessageButtonClick` forwards the
  `user_id` the clicking client sent, so any logged-in user could claim to be someone else. Routes that call
  `RequireServerAuthenticatedActor()` now reject every button click; `WithOwner()` on a button route filters accidental
  clicks but is not a security boundary. Dropdown selections stay `ServerAuthenticated` (the server overwrites the
  user id). `MessageId`, `ChannelId` and `SenderId` of every interaction are client-supplied.
  - Server follow-up (mezon-api, `server/api_interactive_message.go`): build a new `rtapi.MessageButtonClicked` in
    `MessageButtonClick` with `UserId: ctx.UserValue(ctxUserIDKey{}).(int64)`, as `DropdownBoxSelected` does, and
    consider checking channel membership. The SDK cannot detect the server version, so button trust can only be
    restored in a later release once every server is fixed.

### Fixed

- Abridged TCP frames whose envelope ends in `0x00` (an empty sub-message or a zero value) were truncated and dropped;
  the real envelope length is now found by walking the protobuf fields, as the desktop, Android and iOS clients do.
- Realtime frames larger than 8 KiB tore down the connection; the receive cap is now 1 MiB like the other clients.
- Session refresh and logout always failed (they used a REST-only client). Refresh now goes over the socket, falls back
  to re-authenticating with the app credentials, keeps the current session on failure, runs before reconnects, and
  applies `RefreshSessionEvent` pushed by the server. Logging in again while logged in no longer discards the new
  session.
- `GenerateMeetTokenAsync` returned the protobuf reply bytes as the token on current servers; both the protobuf and
  the older raw-JWT replies are decoded.
- Quick menu events from DMs (clan id 0) were dropped, and the parameterless `QuickMenuReceivedEvent` stopped firing
  for incomplete payloads.
- Connection lifecycle: a `Closed` handler that disconnected deadlocked; a concurrent disconnect could deadlock with the
  receive loop; an old WebSocket loop could close a newer connection; a hung connect (e.g. TLS) could not be cancelled
  by the connect timeout or `DisconnectAsync`; disposing during a reconnect backoff left the loop reconnecting; a
  teardown error stopped reconnecting for good; the backoff reset after every connect so a server that accepted and
  closed caused a reconnect per second; the `LoginAsync` token was reused for every reconnect.
- Correlation: response timeouts kept completed requests (and payloads) alive until the timeout; cids could collide at
  the 65535 wrap; pending requests now fail as soon as the connection closes.
- `Latency` stayed 0 over WebSocket because only raw TCP pong frames were timed. Each heartbeat now records its round
  trip when its pong arrives, on either transport. `PongReceivedEvent` is still not raised for heartbeat pongs (it
  never was, since the queue it depended on was never filled), which matches mezon-js.
- Interactions: one-shot routes could run twice under concurrent clicks; expired routes were never removed; handler
  failures were swallowed; replies in uncached channels went to clan 0 with stream mode 0 and cached a placeholder
  channel; fetching a DM channel tried to fetch clan 0.
- `EntityCache.GetOrFetchAsync`: one cancelled caller failed every caller waiting for the same id, and a finishing
  waiter could remove a newer in-flight fetch.
- `ChannelSendQueue` could dispose a gate another sender was using, or create two gates for one channel.
- The SQLite write pump stopped for good after one failed batch, after which `FlushAsync`/`DisposeAsync` hung.
- MMN transfers used the confirmed nonce, so back-to-back transfers collided; the pending nonce is used and transfers
  are serialized.
- Message content: a malformed token (offset outside the text, non-object array item, lone surrogate) made typed
  properties throw on every access; such tokens are now skipped, JSON `null` reads as `null`, and the parsed snapshot
  is published atomically.

### Changed (behaviour)

- **Realtime events are dispatched in order per channel (else per clan)** on `EventDispatchLaneCount` (16) bounded
  lanes of `EventDispatchLaneCapacity` (1024). A handler running longer than `SocketHandlerTimeoutInMilliseconds`
  (default 3000 ms; 30 s when null) stops holding its lane. When a lane is full new events are dropped and counted in
  `MezonClient.DroppedRealtimeEventCount`. `EventDispatchMode.Concurrent` restores the previous behaviour.
- The SDK binds its cache listeners in the constructor, so user handlers see the cache already updated by the event.
- `AsyncEvent` reports several failing subscribers as an `AggregateException` (a single failure keeps its stack trace).
- Receive limits: 1 MiB per realtime frame, 16 MiB per API response (exceeding it fails that request with code
  `0xFFFF`), 16 MiB per WebSocket message (exceeding it closes the connection).
- Three consecutive unauthorized closes stop reconnecting with a critical error. TCP `HTTP/1.1 401` replies and
  WebSocket close codes are now reported to `Closed`/`Disconnected` handlers.
- Interaction handlers resolve uncached channels with `GetChannelAsync` and return `Failed` if that fails.
- `SqliteMessageStore.FlushAsync` can throw the error of a failed write batch instead of hanging.
- MMN initialization failures are logged and no longer fail `LoginAsync`.
- Undecodable realtime frames are reported as rate-limited warnings (without payload bytes).

### Protocol

- Synced with mezon-protocol v2.0.85: `UploadAttachmentRequest.channel_id`/`transcode_hls`, `type_cdn` on upload
  replies, `GenerateMeetTokenRequest.metadata`, `GenerateMeetTokenResponse.url`, `VoiceChannelUser.peer_ids`,
  `VoiceJoinedEvent.peer_id`, `VoiceLeavedEvent.peer_id`.
- New socket APIs `SearchMentionUsersAsync` (index 211) and `GenerateCDNSignatureAsync` (index 212).
- `StreamingServerCallbackAsync` is obsolete: mezon-api no longer handles it.

### Breaking

- Generated `*Params` structs gained optional constructor parameters (for example `UploadAttachmentParams.channelId`,
  `GenerateMeetTokenParams.metadata`). Source-compatible, but already compiled consumers must be rebuilt.
- `MeetParticipantEvent.Action` was removed with the upstream field.

### Packaging

- `SQLitePCLRaw.bundle_e_sqlite3` is pinned to 2.1.13; the transitive 2.1.6/2.1.10/2.1.11 versions were flagged by
  GHSA-2m69-gcr7-jv3q and the `NU1903` warning is no longer suppressed.
- Release builds pass `-p:Version`, so assembly and file versions match the package version.
- `protoc` is resolved per OS/CPU (including linux-arm64) and only when the path exists.

### Upgrading

- **Monze:** bump `Mezon.Net.Sdk`, `.Caching.Redis` and `.Caching.Sqlite` together and refresh the lock files. Private
  button routes using `RequireServerAuthenticatedActor()` return `Unauthorized` until mezon-api is fixed, and the
  contract test that expects button clicks to be server-authenticated must be updated.
- **Mezube:** rebuild against 1.7.0; meet tokens now decode on current servers, and uploads can pass `channelId`.
- A raw event handler that waits for a later event of the same channel (for example awaiting a message collector)
  now holds that channel's lane until the handler timeout. Do such waits in a background task or in SDK command and
  interaction handlers, which already run detached from the lanes.
