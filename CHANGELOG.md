# Changelog

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
