using Mezon.Net.Sdk;

var botId = Environment.GetEnvironmentVariable("MEZON_BOT_ID") ?? string.Empty;
var token = Environment.GetEnvironmentVariable("MEZON_BOT_TOKEN") ?? string.Empty;

if (string.IsNullOrWhiteSpace(botId) || string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Set MEZON_BOT_ID and MEZON_BOT_TOKEN to run the SDK example.");
    return;
}

await using var client = new MezonClient(new MezonClientOptions(botId, token));
client.OnChannelMessage(message =>
{
    Console.WriteLine($"[{message.ChannelId}] {message.Username}: {message.Content}");
    return Task.CompletedTask;
});

if (!await client.LoginAsync())
{
    Console.WriteLine("Login failed.");
    return;
}

Console.WriteLine("Bot connected. Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);
