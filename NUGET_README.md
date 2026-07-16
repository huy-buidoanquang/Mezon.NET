# Mezon.Net

.NET client libraries for the [Mezon](https://mezon.ai) platform — realtime chat, bots, and channel applications.

## Packages

| Package | Audience | Install |
|---------|----------|---------|
| **Mezon.Net.Sdk** | Bot / channel-app developers | `dotnet add package Mezon.Net.Sdk` |
| **Mezon.Net.Client** | UI / full engine access | `dotnet add package Mezon.Net.Client` |
| Mezon.Net.Core | Shared contracts & options | transitive |
| Mezon.Net.Transport | TCP / WebSocket transport | transitive |
| Mezon.Net.Mmn | MMN / ZK HTTP clients | transitive via Sdk |

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

## Rate limits

Socket traffic is throttled client-side (default 60/s, 500/min). Configure limits on `MezonSocketClientOptions`, and optionally handle delays:

```csharp
options.DefaultRatelimitCallback = info =>
{
    Console.WriteLine($"Delayed by {info.Bucket} for {info.ResetAfter}");
    return Task.CompletedTask;
};
```

## Documentation

See the [GitHub repository](https://github.com/Mezon-Net/Mezon.Net) for architecture notes, examples, and contribution guidelines.

## License

MIT
