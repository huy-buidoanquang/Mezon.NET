using Mezon.Net.Sdk;
using Mezon.Net.Sdk.Commands;
using Microsoft.Extensions.Logging;
using MezonLogLevel = Mezon.Net.Logging.LogLevel;

namespace Mezon.Net.Sdk.Example;

internal sealed class MezonBot
{
    private readonly BotOptions _options;
    private readonly ILogger _logger;

    public MezonBot(BotOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var clientOptions = new MezonClientOptions(_options.BotId, _options.Token)
        {
            LogLevel = _options.LogLevel,
            DefaultRatelimitCallback = async info =>
            {
                _logger.LogWarning(
                    "Rate limit bucket={Bucket} global={IsGlobal} limit={Limit} remaining={Remaining} resetAfter={ResetAfter}ms",
                    info.Bucket,
                    info.IsGlobal,
                    info.Limit,
                    info.Remaining,
                    info.ResetAfter.TotalMilliseconds);

                // Optional warning that skips the transport limiter (SDK wires SendBypassMessageAsync):
                // if (info.SendBypassMessageAsync != null)
                //     await info.SendBypassMessageAsync(clanId, channelId, $"Rate limited. Retry in {info.ResetAfter.TotalSeconds:0.#}s");
            },
        };

        await using var client = new MezonClient(clientOptions);
        WireClientLog(client, _logger);

        var commands = new CommandService(_options.CommandPrefix)
        {
            ChannelFilter = _options.ChannelId,
        };

        commands.AddCommand("ping", HandlePingAsync)
            .WithAlias("pong");
        commands.AddCommand("help", HandleHelpAsync);

        client.UseCommands(commands);

        client.Ready += () =>
        {
            _logger.LogInformation(
                "Bot ready. botId={BotId} latency={Latency}ms prefix={Prefix} channelFilter={ChannelFilter}",
                client.BotId,
                client.Latency,
                _options.CommandPrefix,
                _options.ChannelId?.ToString() ?? "*");
            return Task.CompletedTask;
        };

        _logger.LogInformation("Logging in bot {BotId}…", _options.BotId);
        if (!await client.LoginAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogError("Login failed for bot {BotId}.", _options.BotId);
            return 1;
        }

        _logger.LogInformation(
            "Connected. state={State} latency={Latency}ms. Press Ctrl+C to stop.",
            client.ConnectionState,
            client.Latency);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Shutdown requested. Disposing client…");
        }

        return 0;
    }

    private static Task HandlePingAsync(ICommandContext ctx)
        => ctx.ReplyTextAsync($"Pong! latency={ctx.Client.Latency}ms");

    private async Task HandleHelpAsync(ICommandContext ctx)
    {
        var prefix = ctx.Prefix;
        var help =
            $"Commands:\n{prefix}ping — reply with bot latency\n{prefix}help — show this message";

        await ctx.ReplyTextAsync(help).ConfigureAwait(false);
    }

    private static void WireClientLog(MezonClient client, ILogger logger)
    {
        client.Log += message =>
        {
            var text = message.ToString(prependTimestamp: true, timestampKind: DateTimeKind.Utc);
            switch (message.Level)
            {
                case MezonLogLevel.Trace:
                    logger.LogTrace("{MezonLog}", text);
                    break;
                case MezonLogLevel.Debug:
                    logger.LogDebug("{MezonLog}", text);
                    break;
                case MezonLogLevel.Warning:
                    logger.LogWarning("{MezonLog}", text);
                    break;
                case MezonLogLevel.Error:
                case MezonLogLevel.Critical:
                    logger.LogError("{MezonLog}", text);
                    break;
                default:
                    logger.LogInformation("{MezonLog}", text);
                    break;
            }

            return Task.CompletedTask;
        };
    }
}
