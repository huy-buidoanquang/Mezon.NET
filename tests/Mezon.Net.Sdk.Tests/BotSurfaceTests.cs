using System.Reflection;
using Mezon.Net.Sdk;
using Xunit;

namespace Mezon.Net.Sdk.Tests;

public sealed class BotSurfaceTests
{
    [Theory]
    [InlineData(nameof(MezonClient.JoinClanAsync))]
    [InlineData(nameof(MezonClient.ListClanUsersAsync))]
    [InlineData(nameof(MezonClient.ListChannelMessagesAsync))]
    [InlineData(nameof(MezonClient.SendEphemeralMessageToBotAsync))]
    public void Bot_snapshot_methods_are_public(string name)
    {
        Assert.NotNull(typeof(MezonClient).GetMethod(name, BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [InlineData(nameof(MezonClient.Connected))]
    [InlineData(nameof(MezonClient.Disconnected))]
    [InlineData(nameof(MezonClient.Reconnecting))]
    [InlineData(nameof(MezonClient.OwnershipTransferred))]
    public void Bot_lifecycle_events_are_public(string name)
    {
        Assert.NotNull(typeof(MezonClient).GetEvent(name, BindingFlags.Public | BindingFlags.Instance));
    }
}
