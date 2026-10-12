using Mezon.Net.Models;
using Mezon.Net.Sdk;

namespace Mezon.Net.Sdk.Sfu.Tests;

public class MezonClientTokenTests
{
    /// <summary>The snippet of docs/sfu.md: meet tokens for a voice channel from a logged-in Mezon.Net bot.</summary>
    private static TokenProvider VoiceChannelTokens(MezonClient client, long channelId) =>
        TokenProvider.Create(async (request, cancellationToken) =>
            (await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId, null, "Music bot;"))).Token);

    [Fact]
    public void The_documented_mezon_net_token_provider_compiles()
    {
        Func<MezonClient, long, TokenProvider> provider = VoiceChannelTokens;
        Assert.NotNull(provider);
    }
}
