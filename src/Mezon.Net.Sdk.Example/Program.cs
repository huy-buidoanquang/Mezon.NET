using Mezon.Net.Sdk;
using Microsoft.Extensions.Logging;

internal class Program
{
    private const long TargetClanId = 2050100607154393088L;
    private const long TargetChannelId = 2050100608064557059L;

    private static async Task Main(string[] args)
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => { builder.AddConsole(); builder.SetMinimumLevel(LogLevel.Trace); });
        ILogger logger = factory.CreateLogger("Program");

        var botId = 2061341035941859328;
        var token = "ft4Vr4AmhyPSUMaD";
        static void WireClientLog(MezonClient client, ILogger logger)
        {
            client.Log += message =>
            {
                var text = message.ToString(prependTimestamp: true, timestampKind: DateTimeKind.Utc);
                switch (message.Level)
                {
                    case Mezon.Net.Logging.LogLevel.Trace:
                        logger.LogDebug("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Debug:
                        logger.LogDebug("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Warning:
                        logger.LogWarning("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Error:
                    case Mezon.Net.Logging.LogLevel.Critical:
                        logger.LogError("{MezonLog}", text);
                        break;
                    default:
                        logger.LogInformation("{MezonLog}", text);
                        break;
                }

                return Task.CompletedTask;
            };
        }

        var options = new MezonClientOptions(botId, token);
        options.LogLevel = Mezon.Net.Logging.LogLevel.Trace;
        await using var client = new MezonClient(options);
        WireClientLog(client, logger);
        client.ChannelMessageReceived += message =>
        {
            Console.WriteLine($"[{message.ChannelId}] {message.Username}: {message}");
            return Task.CompletedTask;
        };

        if (!await client.LoginAsync())
        {
            Console.WriteLine("Login failed.");
            return;
        }

        try
        {
            var channel = await client.GetChannelAsync(TargetChannelId);
            var ack = await channel.SendAsync("{\"t\":\"12312\"}").ConfigureAwait(false);
            Console.WriteLine($"Sent to clan {TargetClanId}, channel {TargetChannelId}: message_id={ack.MessageId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send failed: {ex.Message}");
            logger.LogError(ex, "Failed to send message to channel {ChannelId}", TargetChannelId);
        }

        Console.WriteLine("Bot connected. Press Ctrl+C to exit.");
        await Task.Delay(Timeout.Infinite);
    }
}
