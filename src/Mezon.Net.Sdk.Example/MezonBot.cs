using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Sdk;
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
            DefaultRatelimitCallback = info =>
            {
                _logger.LogWarning(
                    "Rate limit bucket={Bucket} global={IsGlobal} limit={Limit} remaining={Remaining} resetAfter={ResetAfter}ms",
                    info.Bucket,
                    info.IsGlobal,
                    info.Limit,
                    info.Remaining,
                    info.ResetAfter.TotalMilliseconds);
                return Task.CompletedTask;
            },
        };

        await using var client = new MezonClient(clientOptions);
        WireClientLog(client, _logger);

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

        client.ChannelMessageReceived += evt => OnChannelMessageReceivedAsync(client, evt);

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

    private async Task OnChannelMessageReceivedAsync(MezonClient client, ChannelMessageEventData evt)
    {
        try
        {
            ChannelMessageResponse message = evt;

            if (message.SenderId == client.BotId)
            {
                return;
            }

            if (_options.ChannelId is long allowedChannelId && message.ChannelId != allowedChannelId)
            {
                return;
            }

            var text = MessageContent.ExtractText(message.Content);
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(_options.CommandPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var withoutPrefix = text[_options.CommandPrefix.Length..].TrimStart();
            var command = withoutPrefix.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (command.Length == 0)
            {
                return;
            }

            var name = command[0].ToLowerInvariant();
            _logger.LogInformation(
                "Command {Command} from sender={SenderId} clan={ClanId} channel={ChannelId} message={MessageId} mentions={Mentions} attachments={Attachments} refs={References}",
                name,
                message.SenderId,
                message.ClanId,
                message.ChannelId,
                message.MessageId,
                message.Mentions.Count,
                message.Attachments.Count,
                message.References.Count);

            switch (name)
            {
                case "ping":
                    await HandlePingAsync(client, message).ConfigureAwait(false);
                    break;
                case "help":
                    await HandleHelpAsync(client, message).ConfigureAwait(false);
                    break;
                default:
                    _logger.LogDebug("Ignoring unknown command {Command}", name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while handling ChannelMessageReceived.");
        }
    }

    private async Task HandlePingAsync(MezonClient client, ChannelMessageResponse message)
    {
        var channel = await client.GetChannelAsync(message.ChannelId).ConfigureAwait(false);
        var replyText =
            $"Pong! latency={client.Latency}ms mentions={message.Mentions.Count} attachments={message.Attachments.Count} refs={message.References.Count}";

        await channel.SendAsync(
            MessageContent.BuildTextPayload(replyText),
            references: new[]
            {
                new MessageRefParams(
                    messageRefId: message.MessageId,
                    messageSenderId: message.SenderId,
                    content: message.Content,
                    messageSenderUsername: message.Username,
                    messageSenderAvatar: message.Avatar),
            }).ConfigureAwait(false);
    }

    private async Task HandleHelpAsync(MezonClient client, ChannelMessageResponse message)
    {
        var channel = await client.GetChannelAsync(message.ChannelId).ConfigureAwait(false);
        var prefix = _options.CommandPrefix;
        var help =
            $"Commands:\n{prefix}ping — latency + typed payload counts\n{prefix}help — show this message";

        await channel.SendAsync(
            MessageContent.BuildTextPayload(help),
            references: new[]
            {
                new MessageRefParams(
                    messageRefId: message.MessageId,
                    messageSenderId: message.SenderId,
                    content: message.Content,
                    messageSenderUsername: message.Username,
                    messageSenderAvatar: message.Avatar),
            }).ConfigureAwait(false);
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
