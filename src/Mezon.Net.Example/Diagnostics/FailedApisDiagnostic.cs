using System.Diagnostics;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Example.Infrastructure;
using Mezon.Net.Models;
using Microsoft.Extensions.Logging;

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
            var channel = await client.GetChannelDetailAsync(o.ChannelId, opts).ConfigureAwait(false);
            await client.ListChannelUsersAsync(o.ClanId, o.ChannelId, channel.Type, limit: 20, state: null, cursor: null, options: opts).ConfigureAwait(false);
        }, logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListNotificationsAsync", options, transportType, email, password,
            (client, o, opts) => client.ListNotificationsAsync(o.ClanId, notificationId: null, limit: 10, category: 1, direction: null, options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListRoleUsersAsync", options, transportType, email, password, async (client, o, opts) =>
        {
            if (!roleId.HasValue)
            {
                var rolesResponse = await client.ListRolesAsync(new RoleListEventParams(clanId: o.ClanId, limit: 5), opts).ConfigureAwait(false);
                roleId = rolesResponse.Roles.Roles.Count > 0 ? rolesResponse.Roles.Roles[0].Id : null;
            }

            if (!roleId.HasValue)
            {
                throw new InvalidOperationException("No roles available.");
            }

            await client.ListRoleUsersAsync(new ListRoleUsersParams(roleId: roleId.Value, limit: 10), opts).ConfigureAwait(false);
        }, logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListActivityAsync", options, transportType, email, password,
            (client, _, opts) => client.ListActivityAsync(opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListAppsAsync", options, transportType, email, password,
            (client, _, opts) => client.ListAppsAsync(filter: null, tombstones: null, cursor: null, options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListAuditLogAsync", options, transportType, email, password,
            (client, o, opts) => client.ListAuditLogAsync(o.ClanId, actionLog: null, userId: null, dateLog: null, options: opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("GetChannelCategoryNotificationSettingsAsync", options, transportType, email, password,
            (client, o, opts) => client.GetChannelCategoryNotificationSettingsAsync(o.ClanId, opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("ListChannelDescsAsync", options, transportType, email, password,
            (client, o, opts) => client.ListChannelDescsAsync(new ListChannelDescsParams(clanId: o.ClanId, limit: 20, channelType: 1, page: 0), opts), logger, cancellationToken,
            timeoutMs: options.ListChannelDescsTimeoutMs).ConfigureAwait(false);

        await RunCaseAsync("HealthcheckAsync", options, transportType, email, password,
            (client, _, opts) => client.HealthcheckAsync(opts), logger, cancellationToken).ConfigureAwait(false);

        await RunCaseAsync("SendChannelMessageAsync", options, transportType, email, password, async (client, o, opts) =>
        {
            var channel = await client.GetChannelDetailAsync(o.ChannelId, opts).ConfigureAwait(false);
            var content = System.Text.Json.JsonSerializer.Serialize(new { t = o.TestMessage });
            var isPublic = channel.ChannelPrivate == 0;
            var mode = ChannelStreamModeHelper.FromChannelType(channel.Type);
            await client.JoinChannelChatRtAsync(new ChannelJoinParams(o.ClanId, o.ChannelId, channel.Type, isPublic)).ConfigureAwait(false);
            await client.SendChannelMessageAsync(new SendChannelMessageParams(o.ClanId, o.ChannelId, content, isPublic: isPublic, mode: mode), opts).ConfigureAwait(false);
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
            var session = await client.AuthenticateEmailAsync(ExampleHelpers.CreateEmailAuthRequest(email, password)).ConfigureAwait(false);
            await client.LoginAsync(session).ConfigureAwait(false);
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
