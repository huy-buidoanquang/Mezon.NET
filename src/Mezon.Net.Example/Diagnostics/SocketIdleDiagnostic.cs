using Mezon.Net.Client;
using Mezon.Net.Core;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Verifies whether the dev server closes the socket after ~30s without traffic.
/// </summary>
internal static class SocketIdleDiagnostic
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

        logger.LogInformation("=== Socket idle diagnostic (server ~30s idle hypothesis) ===");

        // Case A: connect then no traffic for 35s
        await RunCase("A: idle 35s (no ping, no API)", async client =>
        {
            await Task.Delay(TimeSpan.FromSeconds(35), cancellationToken).ConfigureAwait(false);
            return client.ConnectionState;
        }, options, transportType, email, password, logger, cancellationToken).ConfigureAwait(false);

        // Case B: connect then rely on automatic heartbeat for 35s
        await RunCase("B: automatic heartbeat for 35s", async client =>
        {
            for (var i = 0; i < 4; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                logger.LogInformation("  tick #{I} state={State} latency={Latency}", i + 1, client.ConnectionState, client.Latency);
            }

            return client.ConnectionState;
        }, options, transportType, email, password, logger, cancellationToken).ConfigureAwait(false);

        // Case C: connect then lightweight API every 1s for 35s
        await RunCase("C: GetAccount every 1s for 35s", async client =>
        {
            for (var i = 0; i < 35; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                _ = await client.ApiClient.GetAccountAsync().ConfigureAwait(false);
                if (i % 10 == 9)
                {
                    logger.LogInformation("  tick {I} state={State}", i + 1, client.ConnectionState);
                }
            }

            return client.ConnectionState;
        }, options, transportType, email, password, logger, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("=== Socket idle diagnostic finished ===");
        logger.LogInformation(
            "Client config: ConnectionTimeout={Conn}ms (handshake only), HeartbeatInterval={Hb}ms",
            MezonSocketClientOptions.DefaultConnectionTimeoutInMilliseconds,
            MezonSocketClientOptions.DefaultHeartbeatIntervalInMilliseconds);
    }

    private static async Task RunCase(
        string title,
        Func<MezonClient, Task<ConnectionState>> idleAction,
        MezonExampleOptions options,
        TransportType transportType,
        string email,
        string password,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("--- {Title} ---", title);
        var clientOptions = new MezonSocketClientOptions(options.Host, options.Port, options.UseSSL)
        {
            TransportType = transportType,
            CreateStatusOnConnect = options.CreateStatusOnConnect,
        };

        await using var client = new MezonClient(clientOptions);
        var session = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
        await client.LoginAsync(session).ConfigureAwait(false);
        await client.ConnectAsync().ConfigureAwait(false);

        logger.LogInformation("  connected state={State}", client.ConnectionState);
        var stateAfter = await idleAction(client).ConfigureAwait(false);
        logger.LogInformation("  after wait state={State}", stateAfter);

        try
        {
            await client.ApiClient.GetAccountAsync().ConfigureAwait(false);
            logger.LogInformation("  post-check GetAccount: OK (socket still usable)");
        }
        catch (Exception ex)
        {
            logger.LogWarning("  post-check GetAccount: FAIL ({Error})", ex.Message);
        }

        await client.DisconnectAsync().ConfigureAwait(false);
    }

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;
}
