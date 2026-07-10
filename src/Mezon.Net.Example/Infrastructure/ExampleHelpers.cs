using Mezon.Net.Client;
using Mezon.Net.Core;
using Microsoft.Extensions.Logging;
using MezonLogLevel = Mezon.Net.Logging.LogLevel;

namespace Mezon.Net.Example.Infrastructure;

internal static class ExampleHelpers
{
    public static (string Email, string Password) ResolveCredentials(MezonExampleOptions options)
    {
        var email = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_EMAIL"), options.Email);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_PASSWORD"), options.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set Mezon:Email and Mezon:Password in appsettings.json or MEZON_EMAIL / MEZON_PASSWORD.");
        }

        return (email, password);
    }

    public static TransportType ResolveTransportType(MezonExampleOptions options)
        => Enum.TryParse(options.TransportType, ignoreCase: true, out TransportType transportType)
            ? transportType
            : TransportType.Tcp;

    public static MezonSocketClientOptions CreateClientOptions(MezonExampleOptions options, MezonLogLevel? logLevel = null)
        => new(options.Host, options.Port, options.UseSSL)
        {
            TransportType = ResolveTransportType(options),
            CreateStatusOnConnect = options.CreateStatusOnConnect,
            LogLevel = logLevel ?? ParseLogLevel(options.SocketLogLevel),
        };

    public static void WireClientLog(MezonClient client, ILogger logger)
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

    public static async Task<MezonClient> ConnectAsync(
        MezonExampleOptions options,
        string email,
        string password,
        ILogger? logger = null,
        MezonLogLevel? logLevel = null,
        CancellationToken cancellationToken = default)
    {
        var client = new MezonClient(CreateClientOptions(options, logLevel));
        if (logger != null)
        {
            WireClientLog(client, logger);
        }

        var session = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
        if (!await client.LoginAsync(session).ConfigureAwait(false))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("LoginAsync returned false.");
        }

        await client.ConnectAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (client.ConnectionState != ConnectionState.Connected)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Socket connection failed. state={client.ConnectionState}");
        }

        return client;
    }

    public static string ResolveMode(MezonExampleOptions options)
        => FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_DIAG"), options.Mode) ?? ExampleModes.Verify;

    public static string FirstNonEmpty(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;

    public static MezonLogLevel ParseLogLevel(string? value)
        => Enum.TryParse(value, ignoreCase: true, out MezonLogLevel level)
            ? level
            : MezonLogLevel.Information;
}

internal static class ExampleModes
{
    public const string Verify = "Verify";
    public const string AllApis = "AllApis";
    public const string ListChannelDescs = "ListChannelDescs";
    public const string SocketIdle = "SocketIdle";
    public const string HeartbeatApi = "HeartbeatApi";
    public const string ListRoles = "ListRoles";
    public const string WireDebug = "WireDebug";
    public const string ListChannelMessages = "ListChannelMessages";
    public const string FailedApis = "FailedApis";
}
