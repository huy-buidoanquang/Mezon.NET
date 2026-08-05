# Events and L1 cache

Sdk past-tense events and what the built-in L1 cache listeners do. Handlers never call REST APIs; membership joins use realtime presence in the background only.

Login seeds **clans + JoinClanChat** only — not every channel/role/message. See [Why login does not seed every entity](../src/Mezon.Net.Sdk/README.md#why-login-does-not-seed-every-entity).

## Membership

| Event | Payload | L1 | Presence |
|-------|---------|----|----------|
| `ClanJoined` | `ClanId` | Stub `Clan` if missing | `JoinClanChat` once per clan (process) |
| `ClanUserAdded` | user + `ClanId` | Upsert `User`; if bot → stub clan | `JoinClanChat` if bot |
| `UserChannelAdded` | `channel_desc` + users | Upsert `Channel` (+ clan stub) | `JoinChannelChat` if bot in users |
| `UserChannelRemoved` | channel + user ids | If bot: `Channels.Remove` | `LeaveChannelChat` if bot |

## Roles

| Event | Payload | L1 |
|-------|---------|----|
| `RoleChanged` | `Role` + `status` + user add/remove ids | Upsert role; `status == 3` removes; apply membership |
| `RoleAssigned` | `ClanId` (string), `RoleId`, assign/remove user ids | Stub role if missing; apply membership only |

## Channels and messages

| Event | L1 |
|-------|----|
| `ChannelCreated` / `ChannelUpdated` | Upsert sparse channel fields |
| `ChannelUpdated` (thread + status 1) | Background `JoinChannelChat` |
| `ChannelDeleted` | `Channels.Remove` |
| `ChannelMessageReceived` | Upsert message **if channel cached**; light user upsert |
| `ChannelMessageUpdated` / `Removed` / reaction | Mutate cached message only |

Message events do **not** create channel stubs and do **not** fetch missing channels.

## App subscribers

Subscribe after L1 for L2/L3 persistence. Keep I/O off the dispatch path. See [caching-l2-l3.md](caching-l2-l3.md).
