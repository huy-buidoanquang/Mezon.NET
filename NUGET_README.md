# Mezon.Net

.NET client libraries for the [Mezon](https://mezon.ai) platform — realtime chat, bots, and channel applications.

## Packages

| Package | Audience | Install |
|---------|----------|---------|
| **Mezon.Net.Sdk** | Bot / channel-app developers | `dotnet add package Mezon.Net.Sdk` |
| **Mezon.Net.Client** | UI / full engine access | `dotnet add package Mezon.Net.Client` |
| Mezon.Net.Core | Shared contracts & options | transitive |
| Mezon.Net.Transport | TCP / WebSocket transport | transitive |
| **Mezon.Net.Mmn** | MMN gRPC + ZK prove HTTP | `dotnet add package Mezon.Net.Mmn` (also transitive from Sdk on net6.0+) |
| Mezon.Net.Sdk.Caching.Sqlite | Optional SQLite message cache | `dotnet add package Mezon.Net.Sdk.Caching.Sqlite` |
| Mezon.Net.Sdk.Caching.Redis | Optional Redis/Valkey snapshot cache | `dotnet add package Mezon.Net.Sdk.Caching.Redis` |

## Quickstart (Sdk)

```csharp
await using var client = new Mezon.Net.Sdk.MezonClient(
    new Mezon.Net.Sdk.MezonClientOptions(botId, token));

client.ChannelMessageReceived += msg =>
{
    Console.WriteLine(msg.Content);
    return Task.CompletedTask;
};

await client.LoginAsync();
var channel = await client.GetChannelAsync(channelId);
await channel.SendAsync("Hello from Mezon.Net");
```

## MMN (Mezon Mainnet)

Configure gRPC node and ZK prove endpoints on `MezonClientOptions` (defaults point at production Mezon endpoints):

```csharp
options.MMNApiUrl = "https://dong.mezon.ai/mmn-api"; // gRPC
options.ZkApiUrl = "https://dong.mezon.ai/zk-api";   // POST /prove
```

After `LoginAsync`, the SDK initializes `KeyGen`, `AddressMmn`, and `ZkProofs`. Send tokens with `SendTransferAsync(recipient, amount)`.

Use `client.Mmn.NodeClient` for low-level gRPC access and `CryptoHelper` for signing. On `netstandard2.1`, MMN APIs are unavailable (Sdk references `Mezon.Net.Mmn` only for net6.0+).

## Rate limits

Socket traffic is throttled client-side (default 60/s, 500/min). Configure limits on `MezonSocketClientOptions`, and optionally handle delays:

```csharp
options.DefaultRatelimitCallback = async info =>
{
    Console.WriteLine($"Delayed by {info.Bucket} for {info.ResetAfter}");

    // SDK wires SendBypassMessageAsync so warnings skip the transport limiter:
    if (info.SendBypassMessageAsync != null)
    {
        await info.SendBypassMessageAsync(clanId, channelId, $"Slow down — retry in {info.ResetAfter.TotalSeconds:0.#}s");
    }
};
```

`RequestOptions.BypassRateLimiter` skips client throttling for a single send; prefer `IRateLimitInfo.SendBypassMessageAsync` for warnings.

## Documentation

See the [GitHub repository](https://github.com/huy-buidoanquang/Mezon.NET) for architecture notes, examples, and contribution guidelines.

## License

MIT
