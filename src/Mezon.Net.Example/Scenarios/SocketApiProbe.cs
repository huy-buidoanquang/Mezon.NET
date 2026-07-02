using Mezon.Net.Api;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Scenarios;

internal static class SocketApiProbe
{
    internal sealed record ProbeContext(
        long ClanId,
        long ChannelId,
        long UserId,
        int ChannelType,
        bool IsPublic,
        string Username);

    internal sealed record ProbeResult(string Name, bool Ok, string? Detail = null, string? Error = null);

    public static Task<IReadOnlyList<ProbeResult>> RunAllAsync(
        MezonClient client,
        ProbeContext ctx,
        MezonExampleOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
        => RunStagedAsync(client, ctx, options, logger, maxStage: 0, cancellationToken);

    public static async Task<IReadOnlyList<ProbeResult>> RunStagedAsync(
        MezonClient client,
        ProbeContext ctx,
        MezonExampleOptions options,
        ILogger logger,
        int maxStage,
        CancellationToken cancellationToken)
    {
        var envStage = Environment.GetEnvironmentVariable("MEZON_PROBE_STAGE");
        if (!string.IsNullOrWhiteSpace(envStage) && int.TryParse(envStage, out var parsed) && parsed > 0)
        {
            maxStage = parsed;
        }
        else if (maxStage <= 0 && options.ProbeMaxStage > 0)
        {
            maxStage = options.ProbeMaxStage;
        }

        var api = client.ApiClient;
        var results = new List<ProbeResult>();
        var opts = new RequestOptions { SocketSendTimeout = options.ApiTimeoutMs };
        var listChannelDescsOpts = new RequestOptions { SocketSendTimeout = options.ListChannelDescsTimeoutMs };
        long? roleId = null;
        long? appId = null;
        long? sentMessageId = null;

        logger.LogInformation(
            "=== Staged socket API probe (maxStage={MaxStage}, delay={Delay}ms, timeout={Timeout}ms) ===",
            maxStage <= 0 ? "all" : maxStage.ToString(),
            options.ApiDelayMs,
            options.ApiTimeoutMs);

        async Task RunStage(int stage, string title, Func<Task> runStage)
        {
            if (maxStage > 0 && stage > maxStage)
            {
                return;
            }

            var stageStart = results.Count;
            logger.LogInformation("--- Stage {Stage}: {Title} ---", stage, title);
            await runStage().ConfigureAwait(false);
            LogStageSummary(stage, title, results, stageStart, client, logger);

            if (maxStage <= 0 || stage < maxStage)
            {
                await StagePauseAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }

        await RunStage(1, "Core reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "GetAccountAsync", () => api.GetAccountAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListClanDescsAsync", () => api.ListClanDescsAsync(new PaginationParams { Limit = 20 }, opts));
        });

        await RunStage(2, "Clan + channel metadata", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListCategoryDescsAsync", () => api.ListCategoryDescsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListFriendsAsync", () => api.ListFriendsAsync(limit: 10, options: opts));
        });

        await RunStage(3, "Roles + events + permissions", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListRolesAsync", async () =>
            {
                var response = await api.ListRolesAsync(ctx.ClanId, limit: 20, options: opts).ConfigureAwait(false);
                if (response.Roles?.Roles.Count > 0)
                {
                    roleId = response.Roles.Roles[0].Id;
                }

                return response;
            });

            await Probe(results, logger, options, cancellationToken, "ListEventsAsync", () => api.ListEventsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetListPermissionAsync", () => api.GetListPermissionAsync(opts));
        });

        await RunStage(4, "Channel reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelMessagesAsync", () => api.ListChannelMessagesAsync(ctx.ClanId, ctx.ChannelId, limit: 10, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelUsersAsync", () => api.ListChannelUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, limit: 20, options: opts));
            await Probe(results, logger, options, cancellationToken, "GetPinMessagesListAsync", () => api.GetPinMessagesListAsync(ctx.ChannelId, ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListNotificationsAsync", () => api.ListNotificationsAsync(ctx.ClanId, limit: 10, category: 1, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListUserPermissionInChannelAsync", () => api.ListUserPermissionInChannelAsync(ctx.ClanId, ctx.ChannelId, opts));
        });

        await RunStage(5, "Extended reads", async () =>
        {
            if (roleId.HasValue)
            {
                await Probe(results, logger, options, cancellationToken, "ListRolePermissionsAsync", () => api.ListRolePermissionsAsync(roleId.Value, opts));
                await Probe(results, logger, options, cancellationToken, "ListRoleUsersAsync", () => api.ListRoleUsersAsync(roleId.Value, limit: 10, options: opts));
            }

            await Probe(results, logger, options, cancellationToken, "GetListEmojisByUserIdAsync", () => api.GetListEmojisByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetListStickersByUserIdAsync", () => api.GetListStickersByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListActivityAsync", () => api.ListActivityAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetUserStatusAsync", () => api.GetUserStatusAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListAppsAsync", async () =>
            {
                var apps = await api.ListAppsAsync(options: opts).ConfigureAwait(false);
                if (apps.Apps.Count > 0)
                {
                    appId = apps.Apps[0].Id;
                }

                return apps;
            });

            if (appId.HasValue)
            {
                await Probe(results, logger, options, cancellationToken, "GetAppAsync", () => api.GetAppAsync(appId.Value, opts));
            }

            await Probe(results, logger, options, cancellationToken, "ListAuditLogAsync", () => api.ListAuditLogAsync(ctx.ClanId, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListBannedUsersAsync", () => api.ListBannedUsersAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelCategoryNotificationSettingsAsync", () => api.GetChannelCategoryNotificationSettingsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetClanNotificationSettingAsync", () => api.GetClanNotificationSettingAsync(ctx.ClanId, opts));
        });

        await RunStage(6, "Slow / problematic reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelDescsAsync", () => api.ListChannelDescsAsync(ctx.ClanId, limit: 50, channelType: ctx.ChannelType, page: 0, options: listChannelDescsOpts));
            await Probe(results, logger, options, cancellationToken, "ListClanUsersAsync", () => api.ListClanUsersAsync(ctx.ClanId, opts));
        });

        await RunStage(7, "Realtime + writes", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "JoinClanChat", () => client.JoinClanChat(ctx.ClanId));
            await Probe(results, logger, options, cancellationToken, "JoinChannelChat", () => client.JoinChannelChat(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, ctx.IsPublic));
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            await Probe(results, logger, options, cancellationToken, "SendRealtimeAsync:MessageTypingEvent", () => client.SendRealtimeAsync(new Envelope
            {
                MessageTypingEvent = new MessageTypingEvent
                {
                    ClanId = ctx.ClanId,
                    ChannelId = ctx.ChannelId,
                    SenderId = ctx.UserId,
                    Mode = 1,
                    IsPublic = ctx.IsPublic,
                    SenderUsername = ctx.Username,
                }
            }, opts));
            await Probe(results, logger, options, cancellationToken, "Ping/Heartbeat", async () =>
            {
                await client.Ping().ConfigureAwait(false);
                return client.Latency;
            });
            await Probe(results, logger, options, cancellationToken, "HealthcheckAsync", () => api.HealthcheckAsync(opts));
            await Probe(results, logger, options, cancellationToken, "SendChannelMessageAsync", async () =>
            {
                var content = System.Text.Json.JsonSerializer.Serialize(new { t = options.TestMessage });
                var mode = Mezon.Net.Api.ChannelStreamModeHelper.FromChannelType(ctx.ChannelType);
                var ack = await api.SendChannelMessageAsync(
                    new SendChannelMessageParams(ctx.ClanId, ctx.ChannelId, content, isPublic: ctx.IsPublic, mode: mode),
                    opts).ConfigureAwait(false);
                sentMessageId = ack.MessageId;
                return ack;
            });

            if (sentMessageId is > 0)
            {
                await Probe(results, logger, options, cancellationToken, "MarkAsReadAsync", () => api.MarkAsReadAsync(new MarkAsReadRequest
                {
                    ClanId = ctx.ClanId,
                    ChannelId = ctx.ChannelId,
                }, opts));
            }
        });

        await RunStage(8, "Account & badges", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListClanBadgeCountAsync", () => api.ListClanBadgeCountAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelBadgeCountAsync", () => api.ListChannelBadgeCountAsync(ctx.ClanId, limit: 20, page: 0, opts));
            await Probe(results, logger, options, cancellationToken, "ListLogedDeviceAsync", () => api.ListLogedDeviceAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListClanUsersStatusAsync", () => api.ListClanUsersStatusAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "EmojiRecentListAsync", () => api.EmojiRecentListAsync(opts));
        });

        await RunStage(9, "Notifications & onboarding", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListMutedChannelAsync", () => api.ListMutedChannelAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetNotificationChannelAsync", () => api.GetNotificationChannelAsync(new NotificationChannel
            {
                ChannelId = ctx.ChannelId,
            }, opts));
            await Probe(results, logger, options, cancellationToken, "GetNotificationCategoryAsync", () => api.GetNotificationCategoryAsync(new DefaultNotificationCategory(), opts));
            await Probe(results, logger, options, cancellationToken, "GetRoleOfUserInTheClanAsync", () => api.GetRoleOfUserInTheClanAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListOnboardingAsync", () => api.ListOnboardingAsync(ctx.ClanId, options: opts));
            await Probe(results, logger, options, cancellationToken, "GetSystemMessageByClanIdAsync", () => api.GetSystemMessageByClanIdAsync(ctx.ClanId, opts));
        });

        await RunStage(10, "Channel extras", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelAppsAsync", () => api.ListChannelAppsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetListFavoriteChannelAsync", () => api.GetListFavoriteChannelAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelAttachmentAsync", () => api.ListChannelAttachmentAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelVoiceUsersAsync", () => api.ListChannelVoiceUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, opts));
            await Probe(results, logger, options, cancellationToken, "ListStreamingChannelUsersAsync", () => api.ListStreamingChannelUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelCanvasListAsync", () => api.GetChannelCanvasListAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelSettingAsync", () => api.ListChannelSettingAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelByUserIdAsync", () => api.ListChannelByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListUserClansByUserIdAsync", () => api.ListUserClansByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetUserProfileOnClanAsync", () => api.GetUserProfileOnClanAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "IsBannedAsync", () => api.IsBannedAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListThreadDescsAsync", () => api.ListThreadDescsAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListArchivedChannelDescsAsync", () => api.ListArchivedChannelDescsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListUserOnlineAsync", () => api.ListUserOnlineAsync(ctx.ClanId, limit: 20, page: 0, opts));
        });

        await RunStage(11, "Misc reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListClanWebhookAsync", () => api.ListClanWebhookAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChanEncryptionMethodAsync", () => api.GetChanEncryptionMethodAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "GetKeyServerAsync", () => api.GetKeyServerAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetPublicKeysAsync", () => api.GetPublicKeysAsync(new[] { ctx.UserId }, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelTimelineAsync", () => api.ListChannelTimelineAsync(new ListChannelTimelineRequest
            {
                ClanId = ctx.ClanId,
                ChannelId = ctx.ChannelId,
            }, opts));
            await Probe(results, logger, options, cancellationToken, "ListOnboardingStepAsync", () => api.ListOnboardingStepAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelDetailAsync", () => api.GetChannelDetailAsync(ctx.ChannelId, opts));
        });

        if (maxStage <= 0 || maxStage >= 11)
        {
            LogSkippedDestructive(results, logger, options.RunDestructiveWrites);
        }

        var executed = results.Where(r => r.Detail != "skipped (destructive)").ToList();
        var ok = executed.Count(r => r.Ok);
        var fail = executed.Count(r => !r.Ok);
        var skipped = results.Count(r => r.Detail == "skipped (destructive)");
        logger.LogInformation("=== Probe summary: {Ok} OK, {Fail} FAIL, {Skip} skipped destructive ===", ok, fail, skipped);

        foreach (var failResult in executed.Where(r => !r.Ok))
        {
            logger.LogWarning("  FAIL {Api}: {Error}", failResult.Name, failResult.Error);
        }

        LogCoverageSummary(results, logger);

        return results;
    }

    private static void LogCoverageSummary(List<ProbeResult> results, ILogger logger)
    {
        var executed = results.Where(r => r.Detail != "skipped (destructive)" && r.Detail != "skipped (write)").ToList();
        var probed = executed.Count;
        var ok = executed.Count(r => r.Ok);
        var fail = executed.Count(r => !r.Ok);
        var skippedDestructive = results.Count(r => r.Detail == "skipped (destructive)");
        var totalMapped = ApiNameIndexMap.NameToIndex.Count;
        var notProbed = totalMapped - probed - skippedDestructive;
        logger.LogInformation(
            "=== Socket API coverage: probed={Probed} ok={Ok} fail={Fail} skipped_write={SkipWrite} map_total={Total} not_probed≈{NotProbed} ===",
            probed,
            ok,
            fail,
            skippedDestructive,
            totalMapped,
            Math.Max(0, notProbed));
    }

    private static void LogStageSummary(int stage, string title, List<ProbeResult> results, int stageStart, MezonClient client, ILogger logger)
    {
        var stageResults = results.Skip(stageStart).Where(r => r.Detail != "skipped (destructive)").ToList();
        var ok = stageResults.Count(r => r.Ok);
        var fail = stageResults.Count(r => !r.Ok);
        logger.LogInformation(
            "=== Stage {Stage} ({Title}) done: {Ok} OK, {Fail} FAIL | socket state={State} latency={Latency}ms ===",
            stage,
            title,
            ok,
            fail,
            client.ConnectionState,
            client.Latency);
    }

    private static async Task StagePauseAsync(MezonExampleOptions options, CancellationToken cancellationToken)
    {
        if (options.StagePauseMs > 0)
        {
            await Task.Delay(options.StagePauseMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private static readonly string[] SkippedDestructive =
    [
        "DeleteAccountAsync", "CreateClanDescAsync", "DeleteClanDescAsync", "UpdateClanDescAsync",
        "RemoveClanUsersAsync", "BanClanUsersAsync", "CreateChannelDescAsync", "DeleteChannelDescAsync",
        "AddFriendsAsync", "BlockFriendsAsync", "DeleteFriendsAsync", "TransferOwnershipAsync",
        "CreateRoleAsync", "DeleteRoleAsync", "CreateEventAsync", "DeleteEventAsync",
        "RegistrationEmailAsync", "UploadAttachmentFileAsync", "ArchiveChannelAsync",
    ];

    private static void LogSkippedDestructive(List<ProbeResult> results, ILogger logger, bool runDestructive)
    {
        if (runDestructive)
        {
            logger.LogWarning("RunDestructiveWrites=true is not implemented; destructive APIs remain skipped.");
        }

        foreach (var name in SkippedDestructive)
        {
            results.Add(new ProbeResult(name, true, Detail: "skipped (destructive)"));
            logger.LogInformation("[SKIP] {Api} (destructive)", name);
        }
    }

    private static async Task Probe(
        List<ProbeResult> results,
        ILogger logger,
        MezonExampleOptions options,
        CancellationToken cancellationToken,
        string name,
        Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            results.Add(new ProbeResult(name, true, "ok"));
            logger.LogInformation("[OK] {Api}", name);
        }
        catch (Exception ex)
        {
            results.Add(new ProbeResult(name, false, Error: ex.Message));
            logger.LogWarning(ex, "[FAIL] {Api}", name);
        }
        finally
        {
            await ProbeDelayAsync(options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task Probe<T>(
        List<ProbeResult> results,
        ILogger logger,
        MezonExampleOptions options,
        CancellationToken cancellationToken,
        string name,
        Func<Task<T>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
            results.Add(new ProbeResult(name, true, "ok"));
            logger.LogInformation("[OK] {Api}", name);
        }
        catch (Exception ex)
        {
            results.Add(new ProbeResult(name, false, Error: ex.Message));
            logger.LogWarning(ex, "[FAIL] {Api}", name);
        }
        finally
        {
            await ProbeDelayAsync(options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ProbeDelayAsync(MezonExampleOptions options, CancellationToken cancellationToken)
    {
        if (options.ApiDelayMs > 0)
        {
            await Task.Delay(options.ApiDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }
}
