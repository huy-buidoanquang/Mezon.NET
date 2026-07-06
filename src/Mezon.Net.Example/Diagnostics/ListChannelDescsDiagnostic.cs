using System.Diagnostics;
using System.Text;
using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Microsoft.Extensions.Logging;

using Mezon.Net.Example.Infrastructure;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Isolated experiments for ListChannelDescsAsync failures (limit, rate limit, api_index=0, chunked response).
/// </summary>
internal static class ListChannelDescsDiagnostic
{
    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var email = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_EMAIL"), options.Email);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_PASSWORD"), options.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set MEZON_EMAIL / MEZON_PASSWORD or appsettings credentials.");
        }

        if (!Enum.TryParse<TransportType>(options.TransportType, ignoreCase: true, out var transportType))
        {
            transportType = TransportType.Tcp;
        }

        var clanId = options.ClanId;
        var delayMs = options.ApiDelayMs;
        var timeoutMs = options.ListChannelDescsTimeoutMs;

        logger.LogInformation(
            "=== ListChannelDescs diagnostic clan={ClanId} delay={Delay}ms timeout={Timeout}ms ===",
            clanId,
            delayMs,
            timeoutMs);

        LogWireFormatSample(logger, clanId);

        await using var client = await ConnectAsync(options, transportType, email, password, logger, cancellationToken).ConfigureAwait(false);
        var api = client.ApiClient;
        var opts = new RequestOptions { SocketSendTimeout = timeoutMs };

        // Experiment 1: first API call after connect (no prior socket API traffic)
        await RunCase(logger, "isolated-first-call limit=5", delayMs, async () =>
        {
            await api.ListChannelDescsAsync(clanId, limit: 5, options: opts).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Experiment 2: vary limit (server proto says 1..100)
        foreach (var limit in new int?[] { null, 1, 10, 50, 100 })
        {
            await RunCase(logger, $"limit={limit?.ToString() ?? "null"}", delayMs, async () =>
            {
                await api.ListChannelDescsAsync(clanId, limit: limit, options: opts).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        // Experiment 3: after burst of other APIs (rate limit hypothesis)
        logger.LogInformation("--- burst 8 quick reads then ListChannelDescs ---");
        for (var i = 0; i < 8; i++)
        {
            await api.GetAccountAsync(opts).ConfigureAwait(false);
            await Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        await RunCase(logger, "after-burst limit=5", delayMs, async () =>
        {
            await api.ListChannelDescsAsync(clanId, limit: 5, options: opts).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Experiment 4: long cooldown then retry
        logger.LogInformation("--- cooldown {Seconds}s then ListChannelDescs ---", options.CooldownSeconds);
        await Task.Delay(TimeSpan.FromSeconds(options.CooldownSeconds), cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "after-cooldown limit=5", delayMs, async () =>
        {
            await api.ListChannelDescsAsync(clanId, limit: 5, options: opts).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Experiment 5: compare ListCategoryDescs (works) vs ListChannelDescs back-to-back
        await RunCase(logger, "ListCategoryDescs control", delayMs, async () =>
        {
            var cats = await api.ListCategoryDescsAsync(clanId, opts).ConfigureAwait(false);
            logger.LogInformation("  control count={Count}", cats.Categorydesc.Count);
        }).ConfigureAwait(false);

        await RunCase(logger, "ListChannelDescs after control", delayMs, async () =>
        {
            var ch = await api.ListChannelDescsAsync(clanId, limit: 5, options: opts).ConfigureAwait(false);
            logger.LogInformation("  channels count={Count}", ch.Channeldesc.Count);
        }).ConfigureAwait(false);

        // Experiment 6: raw request with page=0 via SendApiAsync fields
        await RunCase(logger, "manual request limit=5 page=0 state=0", delayMs, async () =>
        {
            var request = new ListChannelDescsRequest
            {
                ClanId = clanId,
                Limit = 5,
                Page = 0,
                State = 0,
            };
            await client.SendSocketApiAsync("ListChannelDescs", request, ChannelDescList.Parser, opts).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await client.DisconnectAsync().ConfigureAwait(false);
        logger.LogInformation("=== ListChannelDescs diagnostic finished ===");
    }

    private static void LogWireFormatSample(ILogger logger, long clanId)
    {
        var request = new ListChannelDescsRequest { ClanId = clanId, Limit = 5 };
        var requestBytes = request.ToByteArray();

        var envelope = new Envelope
        {
            ApiRequestEvent = new ApiRequestEvent
            {
                ApiIndex = MezonApiMap.TryGetIndex("ListChannelDescs", out var idx) ? idx : -1,
                ApiName = "ListChannelDescs",
                Body = ByteString.CopyFrom(requestBytes),
            },
            Cid = 1,
        };
        var envelopeBytes = envelope.ToByteArray();

        logger.LogInformation(
            "Wire sample api_index={Index} request_bytes={ReqLen} envelope_bytes={EnvLen} request_hex={Hex}",
            envelope.ApiRequestEvent.ApiIndex,
            requestBytes.Length,
            envelopeBytes.Length,
            Convert.ToHexString(requestBytes));
        logger.LogInformation(
            "HasApiIndex={Has} (optional proto should serialize api_index=0 when set)",
            envelope.ApiRequestEvent.HasApiIndex);
    }

    private static async Task RunCase(ILogger logger, string name, int delayMs, Func<Task> action)
    {
        await Task.Delay(delayMs).ConfigureAwait(false);
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
        var auth = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
        logger.LogInformation("Auth OK tcp={TcpUrl}", auth.TcpUrl);

        if (!await client.LoginAsync(new Mezon.Net.Api.Session(auth)).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Login failed.");
        }

        await client.ConnectAsync().ConfigureAwait(false);
        if (client.ConnectionState != ConnectionState.Connected)
        {
            throw new InvalidOperationException($"Not connected: {client.ConnectionState}");
        }

        logger.LogInformation("Socket connected");
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        return client;
    }

    private static Task Delay(int ms, CancellationToken ct) => Task.Delay(ms, ct);

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;
}
