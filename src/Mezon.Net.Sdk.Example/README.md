# Mezon.Net.Sdk.Example

Sample bot host for [`Mezon.Net.Sdk`](../Mezon.Net.Sdk). Demonstrates login, message receive, typed payload inspection (`Mentions` / `Attachments` / `References`), reply-with-reference, logging, and graceful Ctrl+C shutdown.

## Prerequisites

- .NET 8 SDK
- A Mezon bot `BotId` and token

**Do not commit tokens.** Prefer environment variables. If a token was previously hard-coded in source, rotate it.

## Configuration

| Source | Name | Required | Description |
|--------|------|----------|-------------|
| Env / CLI | `MEZON_BOT_ID` / `--bot-id` | Yes | Bot application id |
| Env / CLI | `MEZON_BOT_TOKEN` / `--token` | Yes | Bot token |
| Env / CLI | `MEZON_CHANNEL_ID` / `--channel-id` | No | Only handle commands in this channel |
| Env / CLI | `MEZON_COMMAND_PREFIX` / `--prefix` | No | Command prefix (default `!`) |
| Env / CLI | `MEZON_LOG_LEVEL` / `--log-level` | No | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` |

CLI flags override environment variables.

## Run

PowerShell:

```powershell
$env:MEZON_BOT_ID = "1234567890"
$env:MEZON_BOT_TOKEN = "your-bot-token"
$env:MEZON_CHANNEL_ID = "9876543210"   # optional
dotnet run --project src/Mezon.Net.Sdk.Example -- --prefix ! --log-level Information
```

Bash:

```bash
export MEZON_BOT_ID=1234567890
export MEZON_BOT_TOKEN=your-bot-token
export MEZON_CHANNEL_ID=9876543210   # optional
dotnet run --project src/Mezon.Net.Sdk.Example -- --prefix '!' --log-level Information
```

Help:

```bash
dotnet run --project src/Mezon.Net.Sdk.Example -- --help
```

## In-channel commands

| Command | Action |
|---------|--------|
| `!ping` | Reply with socket latency and typed mention/attachment/reference counts |
| `!help` | List available commands |

Replies use `TextChannel.SendAsync` with a `MessageRefParams` reference to the triggering message.

## Layout

| File | Role |
|------|------|
| `Program.cs` | Composition root, logging, Ctrl+C |
| `BotOptions.cs` | Env + CLI parsing / validation |
| `MezonBot.cs` | Client lifecycle and command handlers |
| `MessageContent.cs` | `{ "t": "..." }` content helpers |

## Notes

- Message bodies are typically JSON (`{"t":"text"}`); the example parses `t` and falls back to raw text.
- Exit codes: `0` success/help, `1` runtime/login failure, `2` invalid/missing configuration.
