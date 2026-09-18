using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Sdk;
using Xunit;

namespace Mezon.Net.Sdk.Tests;

/// <summary>
/// Manual live probe against the Mezon gateway. Run with:
/// MEZON_DEV_PROBE=1 MEZON_BOT_ID=... MEZON_BOT_TOKEN=... MEZON_HOST=... MEZON_PORT=... MEZON_SERVER_KEY=...
/// </summary>
public sealed class GenerateMeetTokenLiveTests
{
    [Fact]
    public async Task GenerateMeetToken_returns_jwt_from_dev_gateway()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEZON_DEV_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var botId = RequireLong("MEZON_BOT_ID");
        var token = Require("MEZON_BOT_TOKEN");
        var host = Require("MEZON_HOST");
        var port = Environment.GetEnvironmentVariable("MEZON_PORT");
        if (string.IsNullOrWhiteSpace(port))
        {
            port = "8088";
        }

        var serverKey = Environment.GetEnvironmentVariable("MEZON_SERVER_KEY");
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            serverKey = "defaultkey";
        }

        var options = new MezonClientOptions(botId, token, host, port, useSSL: true)
        {
            ServerKey = serverKey,
            LogLevel = Mezon.Net.Logging.LogLevel.Information,
        };

        await using var client = new MezonClient(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        Assert.True(await client.LoginAsync(cts.Token).ConfigureAwait(false), "Bot login/connect to gateway failed.");

        var channelId = await ResolveStreamingChannelIdAsync(client, cts.Token).ConfigureAwait(false);
        Assert.True(channelId > 0, "No streaming channel found for this bot.");

        GenerateMeetTokenResponse response;
        try
        {
            response = await client.GenerateMeetTokenAsync(
                    new GenerateMeetTokenParams(channelId, string.Empty),
                    new RequestOptions { CancelToken = cts.Token, SocketSendTimeout = 15_000 })
                .ConfigureAwait(false);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new InvalidOperationException(
                "GenerateMeetToken still parsed the socket payload as protobuf. The gateway returns a raw JWT.",
                ex);
        }

        Assert.False(string.IsNullOrWhiteSpace(response.Token), "Meet token was empty.");
        Assert.StartsWith("eyJ", response.Token, StringComparison.Ordinal);
        Assert.Contains('.', response.Token);
    }

    private static async Task<long> ResolveStreamingChannelIdAsync(MezonClient client, CancellationToken cancellationToken)
    {
        var configured = Environment.GetEnvironmentVariable("MEZON_CHANNEL_ID");
        if (long.TryParse(configured, out var explicitId) && explicitId > 0)
        {
            return explicitId;
        }

        var requestOptions = new RequestOptions
        {
            CancelToken = cancellationToken,
            SocketSendTimeout = 30_000,
        };

        var clans = await client.ListClanDescsAsync(new ListClanDescParams(), requestOptions).ConfigureAwait(false);
        long fallback = 0;
        for (var i = 0; i < clans.Clandesc.Count; i++)
        {
            var clan = clans.Clandesc[i];
            if (clan.WelcomeChannelId != 0)
            {
                return clan.WelcomeChannelId;
            }

            if (fallback == 0 && clan.ClanId != 0)
            {
                fallback = clan.ClanId;
            }
        }

        return fallback;
    }

    private static string Require(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Set {name} before running MEZON_DEV_PROBE=1.");
        }

        return value;
    }

    private static long RequireLong(string name)
    {
        var raw = Require(name);
        return long.TryParse(raw, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid integer for {name}: '{raw}'.");
    }
}
