# Mezon.Net

.NET client libraries for the [Mezon](https://mezon.ai) platform — realtime chat, bots, and channel applications.

## Packages

| Package | Audience | Install |
|---------|----------|---------|
| **Mezon.Net.Sdk** | Bot / channel-app developers | `dotnet add package Mezon.Net.Sdk` |
| **Mezon.Net.Client** | UI / full engine access | `dotnet add package Mezon.Net.Client` |
| Mezon.Net.Core | Shared contracts & options | transitive |
| Mezon.Net.Transport | TCP / WebSocket transport | transitive |
| Mezon.Net.Mmn | MMN gRPC + ZK prove HTTP | not published; bundled in Sdk (net6.0+) |

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

Configure gRPC node and ZK prove endpoints on `MezonClientOptions`:

```csharp
options.MMNApiUrl = "https://your-mmn-grpc-endpoint"; // gRPC, not REST
options.ZkApiUrl = "https://your-zk-prove-service";   // POST /prove
```

After `LoginAsync`, the SDK initializes `KeyGen`, `AddressMmn`, and `ZkProofs`. Send tokens with `SendTransferAsync(recipient, amount)`.

**Breaking change:** MMN no longer uses REST `/sendTransaction` or JSON-RPC nonce. Use `client.Mmn.NodeClient` for low-level gRPC access and `CryptoHelper` for signing.

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

See the [GitHub repository](https://github.com/Mezon-Net/Mezon.Net) for architecture notes, examples, and contribution guidelines.

## License

MIT
