using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Example.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Scenarios;

/// <summary>
/// Default dev flow: auth, socket connect, full API probe, optional heartbeat observe.
/// </summary>
internal static class SocketVerification
{
    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var (email, password) = ExampleHelpers.ResolveCredentials(options);
        var transportType = ExampleHelpers.ResolveTransportType(options);

        await using var client = await ExampleHelpers.ConnectAsync(options, email, password, logger, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        WireEvents(client, logger);

        var clanId = options.ClanId;
        var channelId = options.ChannelId;

        logger.LogInformation(
            "=== Mezon socket verification === host={Host}:{Port} ssl={Ssl} transport={Transport} clan={ClanId} channel={ChannelId}",
            options.Host,
            options.Port,
            options.UseSSL,
            transportType,
            clanId,
            channelId);

        logger.LogInformation("Resolve channel metadata");
        var socketOptions = new RequestOptions { SocketSendTimeout = options.ApiTimeoutMs };
        var account = await client.ApiClient.GetAccountAsync(socketOptions).ConfigureAwait(false);
        var userId = account.User?.Id ?? 0;
        var username = account.User?.Username ?? string.Empty;
        var channel = await client.ApiClient.GetChannelDetailAsync(channelId, socketOptions).ConfigureAwait(false);
        var channelType = channel.Type;
        var isPublic = channel.ChannelPrivate == 0;

        logger.LogInformation(
            "User user_id={UserId} username={Username} channel={Label} type={Type} public={IsPublic}",
            userId,
            username,
            channel.ChannelLabel,
            channelType,
            isPublic);

        logger.LogInformation("Staged socket API probe");
        var probeResults = await SocketApiProbe.RunStagedAsync(
            client,
            new SocketApiProbe.ProbeContext(clanId, channelId, userId, channelType, isPublic, username),
            options,
            logger,
            options.ProbeMaxStage,
            cancellationToken).ConfigureAwait(false);

        var executed = probeResults.Where(r => r.Detail != "skipped (destructive)").ToList();
        var fail = executed.Count(r => !r.Ok);
        if (fail > 0)
        {
            logger.LogWarning("Probe finished with {Fail} failure(s).", fail);
            Environment.ExitCode = 1;
        }

        if (!options.ProbeOnly)
        {
            logger.LogInformation("Post-probe heartbeat observe {Seconds}s...", options.RunSeconds);
            var observeUntil = DateTimeOffset.UtcNow.AddSeconds(options.RunSeconds);
            while (DateTimeOffset.UtcNow < observeUntil && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Heartbeat observe: state={State} latency={Latency}ms",
                    client.ConnectionState,
                    client.Latency);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("=== Socket verification completed ===");

        await client.DisconnectAsync().ConfigureAwait(false);
        try
        {
            await client.LogoutAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LogoutAsync skipped after socket disconnect.");
        }
    }

    private static void WireEvents(MezonClient client, ILogger logger)
    {
        client.Connected += () =>
        {
            logger.LogInformation("Event: Connected");
            return Task.CompletedTask;
        };

        client.ClanJoinedEvent += clanJoin =>
        {
            logger.LogInformation("Event: ClanJoin clan_id={ClanId}", clanJoin.ClanId);
            return Task.CompletedTask;
        };

        client.ChannelJoinedEvent += channelJoin =>
        {
            logger.LogInformation(
                "Event: ChannelJoin clan_id={ClanId} channel_id={ChannelId}",
                channelJoin.ClanId,
                channelJoin.ChannelId);
            return Task.CompletedTask;
        };

        client.ChannelMessageReceivedEvent += message =>
        {
            logger.LogInformation(
                "Event: ChannelMessage channel_id={ChannelId} message_id={MessageId}",
                message.ChannelId,
                message.MessageId);
            return Task.CompletedTask;
        };

        client.ChannelMessageSentEvent += message =>
        {
            logger.LogInformation("Event: ChannelMessageSend channel_id={ChannelId}", message.ChannelId);
            return Task.CompletedTask;
        };

        client.MessageTypingReceivedEvent += typing =>
        {
            logger.LogInformation(
                "Event: MessageTyping channel_id={ChannelId} sender={Sender}",
                typing.ChannelId,
                typing.SenderUsername);
            return Task.CompletedTask;
        };
    }
}
