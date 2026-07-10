using System.Diagnostics;
using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Example.Infrastructure;
using Mezon.Net.Models;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Wire-level verbose for ListRoles vs working APIs. Set Mezon:SocketLogLevel=Trace in appsettings.
/// </summary>
internal static class ListRolesWireDebugDiagnostic
{
    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var email = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_EMAIL"), options.Email);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_PASSWORD"), options.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set credentials in appsettings or MEZON_EMAIL / MEZON_PASSWORD.");
        }

        if (!Enum.TryParse<TransportType>(options.TransportType, ignoreCase: true, out var transportType))
        {
            transportType = TransportType.Tcp;
        }

        var clanId = options.ClanId;
        var timeoutMs = options.ApiTimeoutMs;

        LogRequestWireSample(logger, clanId);

        logger.LogInformation(
            "=== ListRoles wire verbose (SocketLogLevel=Trace) timeout={Timeout}ms clan={ClanId} ===",
            timeoutMs,
            clanId);

        var clientOptions = new MezonSocketClientOptions(options.Host, options.Port, options.UseSSL)
        {
            TransportType = transportType,
            CreateStatusOnConnect = options.CreateStatusOnConnect,
            LogLevel = Mezon.Net.Logging.LogLevel.Trace,
        };

        await using var client = new MezonClient(clientOptions);
        client.Log += message =>
        {
            logger.LogInformation("{MezonLog}", message.ToString(prependTimestamp: true, timestampKind: DateTimeKind.Utc));
            return Task.CompletedTask;
        };

        var session = await client.AuthenticateEmailAsync(ExampleHelpers.CreateEmailAuthRequest(email, password)).ConfigureAwait(false);
        await client.LoginAsync(session).ConfigureAwait(false);
        await client.ConnectAsync().ConfigureAwait(false);

        var opts = new RequestOptions { SocketSendTimeout = timeoutMs };

        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "GetAccount (control)", () => client.GetAccountAsync(opts)).ConfigureAwait(false);
        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "ListFriends (control)", () => client.ListFriendsAsync(state: null, limit: 10, cursor: null, options: opts)).ConfigureAwait(false);
        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "ListRoles (failing)", () => client.ListRolesAsync(new RoleListEventParams(clanId: clanId, limit: 20), opts)).ConfigureAwait(false);
        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        await RunCase(logger, "GetAccount (after ListRoles)", () => client.GetAccountAsync(opts)).ConfigureAwait(false);
        await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Post-fail state: connected={Connected} pending={Pending} latency={Latency}ms",
            client.ConnectionState,
            client.PendingSocketRequestCount,
            client.Latency);

        await client.DisconnectAsync().ConfigureAwait(false);
        logger.LogInformation("=== ListRoles wire debug finished ===");
    }

    private static void LogRequestWireSample(ILogger logger, long clanId)
    {
        var request = new RoleListEventRequest { ClanId = clanId, Limit = 20 };
        var requestBytes = request.ToByteArray();
        MezonApiMap.TryGetIndex("ListRoles", out var index);

        var envelope = new Envelope
        {
            Cid = 42,
            ApiRequestEvent = new ApiRequestEvent
            {
                ApiIndex = index,
                ApiName = "ListRoles",
                Body = ByteString.CopyFrom(requestBytes),
            },
        };

        logger.LogInformation(
            "ListRoles wire: api_index={Index} body_bytes={BodyLen} envelope_bytes={EnvLen} body_hex={Hex}",
            index,
            requestBytes.Length,
            envelope.ToByteArray().Length,
            Convert.ToHexString(requestBytes));
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

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;
}
