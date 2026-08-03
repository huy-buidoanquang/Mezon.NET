using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Models;
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
            await Probe(results, logger, options, cancellationToken, "GetAccountAsync", () => client.GetAccountAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListClanDescsAsync", () => client.ListClanDescsAsync(new ListClanDescParams(limit: 20), opts));
        });

        await RunStage(2, "Clan + channel metadata", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListCategoryDescsAsync", () => client.ListCategoryDescsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListFriendsAsync", () => client.ListFriendsAsync(state: null, limit: 10, cursor: null, options: opts));
        });

        await RunStage(3, "Roles + events + permissions", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListRolesAsync", async () =>
            {
                var response = await client.ListRolesAsync(new RoleListEventParams(clanId: ctx.ClanId, limit: 20), opts).ConfigureAwait(false);
                if (response.Roles.Roles.Count > 0)
                {
                    roleId = response.Roles.Roles[0].Id;
                }

                return response;
            });

            await Probe(results, logger, options, cancellationToken, "ListEventsAsync", () => client.ListEventsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetListPermissionAsync", () => client.GetListPermissionAsync(opts));
        });

        await RunStage(4, "Channel reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelMessagesAsync", () => client.ListChannelMessagesAsync(ctx.ClanId, ctx.ChannelId, messageId: null, direction: null, limit: 10, topicId: null, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelUsersAsync", () => client.ListChannelUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, limit: 20, state: null, cursor: null, options: opts));
            await Probe(results, logger, options, cancellationToken, "GetPinMessagesListAsync", () => client.GetPinMessagesListAsync(ctx.ChannelId, ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListNotificationsAsync", () => client.ListNotificationsAsync(ctx.ClanId, notificationId: null, limit: 10, category: 1, direction: null, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListUserPermissionInChannelAsync", () => client.ListUserPermissionInChannelAsync(ctx.ClanId, ctx.ChannelId, opts));
        });

        await RunStage(5, "Extended reads", async () =>
        {
            if (roleId.HasValue)
            {
                await Probe(results, logger, options, cancellationToken, "ListRolePermissionsAsync", () => client.ListRolePermissionsAsync(roleId.Value, opts));
                await Probe(results, logger, options, cancellationToken, "ListRoleUsersAsync", () => client.ListRoleUsersAsync(new ListRoleUsersParams(roleId: roleId.Value, limit: 10), opts));
            }

            await Probe(results, logger, options, cancellationToken, "GetListEmojisByUserIdAsync", () => client.GetListEmojisByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetListStickersByUserIdAsync", () => client.GetListStickersByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListActivityAsync", () => client.ListActivityAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetUserStatusAsync", () => client.GetUserStatusAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListAppsAsync", async () =>
            {
                var apps = await client.ListAppsAsync(filter: null, tombstones: null, cursor: null, options: opts).ConfigureAwait(false);
                if (apps.Apps.Count > 0)
                {
                    appId = apps.Apps[0].Id;
                }

                return apps;
            });

            if (appId.HasValue)
            {
                await Probe(results, logger, options, cancellationToken, "GetAppAsync", () => client.GetAppAsync(appId.Value, opts));
            }

            await Probe(results, logger, options, cancellationToken, "ListAuditLogAsync", () => client.ListAuditLogAsync(ctx.ClanId, actionLog: null, userId: null, dateLog: null, options: opts));
            await Probe(results, logger, options, cancellationToken, "ListBannedUsersAsync", () => client.ListBannedUsersAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelCategoryNotificationSettingsAsync", () => client.GetChannelCategoryNotificationSettingsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetClanNotificationSettingAsync", () => client.GetClanNotificationSettingAsync(ctx.ClanId, opts));
        });

        await RunStage(6, "Slow / problematic reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelDescsAsync", () => client.ListChannelDescsAsync(new ListChannelDescsParams(clanId: ctx.ClanId, limit: 50, channelType: ctx.ChannelType, page: 0), listChannelDescsOpts));
            await Probe(results, logger, options, cancellationToken, "ListClanUsersAsync", () => client.ListClanUsersAsync(ctx.ClanId, opts));
        });

        await RunStage(7, "Realtime + writes", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "JoinClanChatRtAsync", () => client.JoinClanChatRtAsync(new ClanJoinParams(ctx.ClanId)));
            await Probe(results, logger, options, cancellationToken, "JoinChannelChatRtAsync", () => client.JoinChannelChatRtAsync(new ChannelJoinParams(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, ctx.IsPublic)));
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            results.Add(new ProbeResult("SendMessageTypingRtAsync", true, Detail: "skipped (optional)"));
            logger.LogInformation("[SKIP] SendMessageTypingRtAsync (optional smoke)");
            await ProbeDelayAsync(options, cancellationToken).ConfigureAwait(false);
            await Probe(results, logger, options, cancellationToken, "Latency (automatic heartbeat)", async () => client.Latency);
            await Probe(results, logger, options, cancellationToken, "HealthcheckAsync", () => client.HealthcheckAsync(opts));
            await Probe(results, logger, options, cancellationToken, "SendChannelMessageAsync", async () =>
            {
                var content = System.Text.Json.JsonSerializer.Serialize(new { t = options.TestMessage });
                var mode = Mezon.Net.Client.ChannelStreamModeHelper.FromChannelType(ctx.ChannelType);
                var ack = await client.SendChannelMessageAsync(
                    new SendChannelMessageParams(ctx.ClanId, ctx.ChannelId, content, isPublic: ctx.IsPublic, mode: mode),
                    opts).ConfigureAwait(false);
                sentMessageId = ack.MessageId;
                return ack;
            });

            if (sentMessageId is > 0)
            {
                await Probe(results, logger, options, cancellationToken, "MarkAsReadAsync", () => client.MarkAsReadAsync(new MarkAsReadParams(channelId: ctx.ChannelId, clanId: ctx.ClanId), opts));
            }
        });

        await RunStage(8, "Account & badges", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListClanBadgeCountAsync", () => client.ListClanBadgeCountAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelBadgeCountAsync", () => client.ListChannelBadgeCountAsync(ctx.ClanId, limit: 20, page: 0, opts));
            await Probe(results, logger, options, cancellationToken, "ListLogedDeviceAsync", () => client.ListLogedDeviceAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListClanUsersStatusAsync", () => client.ListClanUsersStatusAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "EmojiRecentListAsync", () => client.EmojiRecentListAsync(opts));
        });

        await RunStage(9, "Notifications & onboarding", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListMutedChannelAsync", () => client.ListMutedChannelAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetNotificationChannelAsync", () => client.GetNotificationChannelAsync(new NotificationChannelParams(channelId: ctx.ChannelId), opts));
            await Probe(results, logger, options, cancellationToken, "GetNotificationCategoryAsync", () => client.GetNotificationCategoryAsync(new DefaultNotificationCategoryParams(), opts));
            await Probe(results, logger, options, cancellationToken, "GetRoleOfUserInTheClanAsync", () => client.GetRoleOfUserInTheClanAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListOnboardingAsync", () => client.ListOnboardingAsync(ctx.ClanId, guideType: null, options: opts));
            await Probe(results, logger, options, cancellationToken, "GetSystemMessageByClanIdAsync", () => client.GetSystemMessageByClanIdAsync(ctx.ClanId, opts));
        });

        await RunStage(10, "Channel extras", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListChannelAppsAsync", () => client.ListChannelAppsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetListFavoriteChannelAsync", () => client.GetListFavoriteChannelAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelAttachmentAsync", () => client.ListChannelAttachmentAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelVoiceUsersAsync", () => client.ListChannelVoiceUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, opts));
            await Probe(results, logger, options, cancellationToken, "ListStreamingChannelUsersAsync", () => client.ListStreamingChannelUsersAsync(ctx.ClanId, ctx.ChannelId, ctx.ChannelType, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelCanvasListAsync", () => client.GetChannelCanvasListAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelSettingAsync", () => client.ListChannelSettingAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelByUserIdAsync", () => client.ListChannelByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "ListUserClansByUserIdAsync", () => client.ListUserClansByUserIdAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetUserProfileOnClanAsync", () => client.GetUserProfileOnClanAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "IsBannedAsync", () => client.IsBannedAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListThreadDescsAsync", () => client.ListThreadDescsAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "ListArchivedChannelDescsAsync", () => client.ListArchivedChannelDescsAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "ListUserOnlineAsync", () => client.ListUserOnlineAsync(ctx.ClanId, limit: 20, page: 0, opts));
        });

        await RunStage(11, "Misc reads", async () =>
        {
            await Probe(results, logger, options, cancellationToken, "ListClanWebhookAsync", () => client.ListClanWebhookAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChanEncryptionMethodAsync", () => client.GetChanEncryptionMethodAsync(ctx.ChannelId, opts));
            await Probe(results, logger, options, cancellationToken, "GetKeyServerAsync", () => client.GetKeyServerAsync(opts));
            await Probe(results, logger, options, cancellationToken, "GetPublicKeysAsync", () => client.GetPublicKeysAsync(new[] { ctx.UserId }, opts));
            await Probe(results, logger, options, cancellationToken, "ListChannelTimelineAsync", () => client.ListChannelTimelineAsync(new ListChannelTimelineParams(clanId: ctx.ClanId, channelId: ctx.ChannelId), opts));
            await Probe(results, logger, options, cancellationToken, "ListOnboardingStepAsync", () => client.ListOnboardingStepAsync(ctx.ClanId, opts));
            await Probe(results, logger, options, cancellationToken, "GetChannelDetailAsync", () => client.GetChannelDetailAsync(ctx.ChannelId, opts));
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
        var totalMapped = MezonApiMap.NameToIndex.Count;
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
