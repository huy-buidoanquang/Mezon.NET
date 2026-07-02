using System.Diagnostics;
using Mezon.Net.Api;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Microsoft.Extensions.Logging;

using Mezon.Net.Example.Infrastructure;

namespace Mezon.Net.Example.Diagnostics;

/// <summary>
/// Tests previously-failing socket APIs one-by-one on a fresh connection.
/// </summary>
internal static class FailedApisDiagnostic
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

        logger.LogInformation("=== Failed APIs diagnostic (fresh connection per API) ===");

        long? roleId = null;

        await RunCaseAsync("ListChannelUsersAsync", options, transportType, email, password, async (client, o, opts) =>
        {
            var api = client.ApiClient;
            var channel = await api.GetChannelDetailAsync(o.ChannelId, opts).ConfigureAwait(false);
            await api.ListChannelUsersAsync(o.ClanId, o.ChannelId, channel.Type, limit: 20, options: opts).ConfigureAwait(false);
        }, logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListNotificationsAsync", options, transportType, email, password,
            (client, o, opts) => client.ApiClient.ListNotificationsAsync(o.ClanId, limit: 10, category: 1, options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListRoleUsersAsync", options, transportType, email, password, async (client, o, opts) =>
        {
            var api = client.ApiClient;
            roleId ??= (await api.ListRolesAsync(o.ClanId, limit: 5, options: opts).ConfigureAwait(false))
                .Roles?.Roles.FirstOrDefault()?.Id;
            if (!roleId.HasValue)
            {
                throw new InvalidOperationException("No roles available.");
            }

            await api.ListRoleUsersAsync(roleId.Value, limit: 10, options: opts).ConfigureAwait(false);
        }, logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListActivityAsync", options, transportType, email, password,
            (client, _, opts) => client.ApiClient.ListActivityAsync(opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListAppsAsync", options, transportType, email, password,
            (client, _, opts) => client.ApiClient.ListAppsAsync(options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListAuditLogAsync", options, transportType, email, password,
            (client, o, opts) => client.ApiClient.ListAuditLogAsync(o.ClanId, options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("GetChannelCategoryNotificationSettingsAsync", options, transportType, email, password,
            (client, o, opts) => client.ApiClient.GetChannelCategoryNotificationSettingsAsync(o.ClanId, opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListChannelDescsAsync", options, transportType, email, password,
            (client, o, opts) => client.ApiClient.ListChannelDescsAsync(o.ClanId, limit: 20, channelType: 1, page: 0, options: opts), logger, cancellationToken,
            timeoutMs: options.ListChannelDescsTimeoutMs).ConfigureAwait(false);

        await RunCaseAsync("HealthcheckAsync", options, transportType, email, password,
            (client, _, opts) => client.ApiClient.HealthcheckAsync(opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("SendChannelMessageAsync", options, transportType, email, password, async (client, o, opts) =>
        {
            var api = client.ApiClient;
            var channel = await api.GetChannelDetailAsync(o.ChannelId, opts).ConfigureAwait(false);
            var content = System.Text.Json.JsonSerializer.Serialize(new { t = o.TestMessage });
            var isPublic = channel.ChannelPrivate == 0;
            var mode = ChannelStreamModeHelper.FromChannelType(channel.Type);
            await client.JoinChannelChat(o.ClanId, o.ChannelId, channel.Type, isPublic).ConfigureAwait(false);
            await api.SendChannelMessageAsync(new SendChannelMessageParams(o.ClanId, o.ChannelId, content, isPublic: isPublic, mode: mode), opts).ConfigureAwait(false);
        }, logger, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("=== Failed APIs diagnostic finished ===");
    }

    private static async Task RunCaseAsync(
        string name,
        MezonExampleOptions options,
        TransportType transportType,
        string email,
        string password,
        Func<MezonClient, MezonExampleOptions, RequestOptions, Task> run,
        ILogger logger,
        CancellationToken cancellationToken,
        int? timeoutMs = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var clientOptions = new MezonSocketClientOptions(options.Host, options.Port, options.UseSSL)
            {
                TransportType = transportType,
                CreateStatusOnConnect = options.CreateStatusOnConnect,
            };

            await using var client = new MezonClient(clientOptions);
            var auth = await client.AuthenticateEmailAsync(email, password).ConfigureAwait(false);
            await client.LoginAsync(new Mezon.Net.Api.Session(auth)).ConfigureAwait(false);
            await client.ConnectAsync().ConfigureAwait(false);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            var opts = new RequestOptions { SocketSendTimeout = timeoutMs ?? options.ApiTimeoutMs };
            await run(client, options, opts).ConfigureAwait(false);

            sw.Stop();
            logger.LogInformation("[OK] {Name} elapsed_ms={Ms}", name, sw.ElapsedMilliseconds);
            await client.DisconnectAsync().ConfigureAwait(false);
            await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning("[FAIL] {Name} elapsed_ms={Ms} error={Error}", name, sw.ElapsedMilliseconds, ex.Message);
            await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string FirstNonEmpty(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : b ?? string.Empty;
}
