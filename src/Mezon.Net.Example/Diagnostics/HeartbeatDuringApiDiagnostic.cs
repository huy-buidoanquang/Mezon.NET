using Mezon.Net.Client;
using Mezon.Net.Core;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Verifies background heartbeat keeps working while a socket API call is in flight (mezon-js concurrent cIds model).
/// </summary>
internal static class HeartbeatDuringApiDiagnostic
{
    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var email = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_EMAIL"), options.Email);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_PASSWORD"), options.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set MEZON_EMAIL / MEZON_PASSWORD.");
        }

        if (!Enum.TryParse<TransportType>(options.TransportType, ignoreCase: true, out var transportType))
        {
            transportType = TransportType.Tcp;
        }

        logger.LogInformation("=== Heartbeat + API concurrent diagnostic ===");

        var clientOptions = new MezonSocketClientOptions(options.Host, options.Port, options.UseSSL)
        {
            TransportType = transportType,
            CreateStatusOnConnect = options.CreateStatusOnConnect,
            LogLevel = Mezon.Net.Logging.LogLevel.Information,
        };

        await using var client = new MezonClient(clientOptions);
        var heartbeatErrors = 0;
        client.Log += message =>
        {
            if (message.Message.Contains("Heartbeat Errored", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref heartbeatErrors);
            }

            logger.LogInformation("{MezonLog}", message.ToString(prependTimestamp: true, timestampKind: DateTimeKind.Utc));
            return Task.CompletedTask;
        };

        var auth = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
        await client.LoginAsync(new Mezon.Net.Api.Session(auth)).ConfigureAwait(false);
        await client.ConnectAsync().ConfigureAwait(false);

        var api = client.ApiClient;
        var opts = new RequestOptions { SocketSendTimeout = options.ApiTimeoutMs };

        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitor = Task.Run(async () =>
        {
            while (!monitorCts.Token.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Monitor: state={State} latency={Latency}ms pending={Pending}",
                    client.ConnectionState,
                    client.Latency,
                    client.PendingSocketRequestCount);
                await Task.Delay(3000, monitorCts.Token).ConfigureAwait(false);
            }
        }, monitorCts.Token);

        logger.LogInformation("Phase A: heartbeat-only {Seconds}s", options.RunSeconds / 2);
        await Task.Delay(TimeSpan.FromSeconds(options.RunSeconds / 2), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Phase B: GetAccountAsync (sanity)...");
        try
        {
            _ = await api.GetAccountAsync(opts).ConfigureAwait(false);
            logger.LogInformation("Phase B: GetAccountAsync OK");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Phase B: GetAccountAsync FAIL");
        }

        logger.LogInformation("Phase C: ListRolesAsync while heartbeat loop runs (concurrent cIds)...");
        try
        {
            _ = await api.ListRolesAsync(options.ClanId, limit: 20, options: opts).ConfigureAwait(false);
            logger.LogInformation("Phase C: ListRolesAsync OK");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Phase C: ListRolesAsync FAIL");
        }

        logger.LogInformation("Phase D: ListCategoryDescsAsync...");
        try
        {
            _ = await api.ListCategoryDescsAsync(options.ClanId, opts).ConfigureAwait(false);
            logger.LogInformation("Phase D: ListCategoryDescsAsync OK");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Phase D: ListCategoryDescsAsync FAIL");
        }

        logger.LogInformation("Phase E: observe {Seconds}s after API calls", options.RunSeconds / 2);
        await Task.Delay(TimeSpan.FromSeconds(options.RunSeconds / 2), cancellationToken).ConfigureAwait(false);

        monitorCts.Cancel();
        try
        {
            await monitor.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        logger.LogInformation(
            "=== Diagnostic done: heartbeat_errors={Errors} final_state={State} latency={Latency}ms ===",
            heartbeatErrors,
            client.ConnectionState,
            client.Latency);

        await client.DisconnectAsync().ConfigureAwait(false);
    }

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;
}
