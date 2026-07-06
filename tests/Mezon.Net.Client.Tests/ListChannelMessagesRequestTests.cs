using Google.Protobuf;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;

namespace Mezon.Net.Client.Tests;

public class ListChannelMessagesRequestTests
{
    private const long ClanId = 2042062935735406592;
    private const long ChannelId = 2042062936049979392;

    [Fact]
    public void WireEncoding_MatchesMezonJs_MinimalRequest()
    {
        var request = new ListChannelMessagesRequest
        {
            ClanId = ClanId,
            ChannelId = ChannelId,
            Limit = 10,
        };

        // field 1 clan_id, field 2 channel_id, field 4 limit (message_id/direction omitted)
        Assert.Equal("0880A080ACBBADB7AB1C1080A080C2BCADB7AB1C200A", Convert.ToHexString(request.ToByteArray()));
    }

    [Fact]
    public void ApiIndex_MatchesMezonJs()
    {
        Assert.True(MezonApiMap.TryGetIndex("ListChannelMessages", out var index));
        Assert.Equal(30, index);
    }
}
