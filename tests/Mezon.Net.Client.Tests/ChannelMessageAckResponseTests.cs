using Mezon.Net.Models;
using Xunit;

namespace Mezon.Net.Client.Tests;

public sealed class ChannelMessageAckResponseTests
{
    [Fact]
    public void Default_response_has_safe_scalar_values()
    {
        var response = default(ChannelMessageAckResponse);

        Assert.Equal(0L, response.ChannelId);
        Assert.Equal(0L, response.MessageId);
        Assert.Equal(0, response.Code);
        Assert.Equal(string.Empty, response.Username);
        Assert.Equal(0U, response.CreateTimeSeconds);
        Assert.Equal(0U, response.UpdateTimeSeconds);
        Assert.False(response.Persistent);
        Assert.Equal(string.Empty, response.ClanLogo);
        Assert.Equal(string.Empty, response.CategoryName);
    }
}
