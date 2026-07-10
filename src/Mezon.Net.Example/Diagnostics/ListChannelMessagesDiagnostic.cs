using System.Diagnostics;
using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Verifies ListChannelMessages request wire format vs mezon-js and response shape.
/// </summary>
internal static class ListChannelMessagesDiagnostic
{
    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var email = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_EMAIL"), options.Email);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_PASSWORD"), options.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set credentials in appsettings.");
        }

        if (!Enum.TryParse<TransportType>(options.TransportType, ignoreCase: true, out var transportType))
        {
            transportType = TransportType.Tcp;
        }

        var clanId = options.ClanId;
        var channelId = options.ChannelId;
        var opts = new RequestOptions { SocketSendTimeout = options.ApiTimeoutMs };

        LogWireVariants(logger, clanId, channelId, limit: 10);

        await using var client = await ConnectAsync(options, transportType, email, password, logger, cancellationToken).ConfigureAwait(false);
        var api = client.ApiClient;

        await RunCase(logger, "minimal (clan+channel+limit)", async () =>
        {
            var r = await api.ListChannelMessagesAsync(clanId, channelId, limit: 10, options: opts).ConfigureAwait(false);
            logger.LogInformation("  messages={Count} last_seen={LastSeen} last_sent={LastSent}",
                r.Messages.Count, r.LastSeenMessage?.Id, r.LastSentMessage?.Id);
            if (r.Messages.Count > 0)
            {
                var m = r.Messages[0];
                logger.LogInformation(
                    "  first: id={Id} sender={Sender} channel={Channel} content_len={Len} content_preview={Preview}",
                    m.MessageId, m.SenderId, m.ChannelId, m.Content?.Length ?? 0,
                    Truncate(m.Content, 80));
            }
        }).ConfigureAwait(false);

        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "with direction=1 (older→newer per proto comment)", async () =>
        {
            var r = await api.ListChannelMessagesAsync(clanId, channelId, direction: 1, limit: 10, options: opts).ConfigureAwait(false);
            logger.LogInformation("  messages={Count}", r.Messages.Count);
        }).ConfigureAwait(false);

        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "with direction=0", async () =>
        {
            var r = await api.ListChannelMessagesAsync(clanId, channelId, direction: 0, limit: 10, options: opts).ConfigureAwait(false);
            logger.LogInformation("  messages={Count}", r.Messages.Count);
        }).ConfigureAwait(false);

        await client.DisconnectAsync().ConfigureAwait(false);
        logger.LogInformation("=== ListChannelMessages diagnostic finished ===");
    }

    private static void LogWireVariants(ILogger logger, long clanId, long channelId, int limit)
    {
        MezonApiMap.TryGetIndex("ListChannelMessages", out var index);

        var minimal = new ListChannelMessagesRequest { ClanId = clanId, ChannelId = channelId, Limit = limit };
        var withDirection = new ListChannelMessagesRequest { ClanId = clanId, ChannelId = channelId, Limit = limit, Direction = 1 };

        foreach (var (name, req) in new[] { ("minimal", minimal), ("direction=1", withDirection) })
        {
            var body = req.ToByteArray();
            var envelope = new Envelope
            {
                Cid = 1,
                ApiRequestEvent = new ApiRequestEvent
                {
                    ApiIndex = index,
                    ApiName = "ListChannelMessages",
                    Body = ByteString.CopyFrom(body),
                },
            };

            logger.LogInformation(
                "{Name}: api_index={Index} body_hex={Hex} body_len={Len} json={Json}",
                name,
                index,
                Convert.ToHexString(body),
                body.Length,
                req.ToString());
        }
    }

    private static async Task RunCase(ILogger logger, string name, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action().ConfigureAwait(false);
            sw.Stop();
            logger.LogInformation("[OK] {Case} elapsed_ms={Ms}", name, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning("[FAIL] {Case} elapsed_ms={Ms} error={Error}", name, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private static async Task<MezonClient> ConnectAsync(
        MezonExampleOptions options,
        TransportType transportType,
        string email,
        string password,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var clientOptions = new MezonSocketClientOptions(options.Host, options.Port, options.UseSSL)
        {
            TransportType = transportType,
            CreateStatusOnConnect = options.CreateStatusOnConnect,
        };

        var client = new MezonClient(clientOptions);
        var session = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
        await client.LoginAsync(session).ConfigureAwait(false);
        await client.ConnectAsync().ConfigureAwait(false);
        logger.LogInformation("Connected state={State}", client.ConnectionState);
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        return client;
    }

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}
