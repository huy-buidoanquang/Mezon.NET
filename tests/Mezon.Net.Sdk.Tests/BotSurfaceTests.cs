using System;
using System.Reflection;
using System.Threading.Tasks;
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
    [InlineData(nameof(MezonClient.ListQuickMenuAccessAsync))]
    public void Bot_snapshot_methods_are_public(string name)
    {
        Assert.NotNull(typeof(MezonClient).GetMethod(name, BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [InlineData(nameof(MezonClient.Connected))]
    [InlineData(nameof(MezonClient.Disconnected))]
    [InlineData(nameof(MezonClient.Reconnecting))]
    [InlineData(nameof(MezonClient.OwnershipTransferred))]
    [InlineData(nameof(MezonClient.QuickMenuReceivedData))]
    [InlineData(nameof(MezonClient.SfuBreakoutRoomReceived))]
    [InlineData(nameof(MezonClient.FriendStateUpdated))]
    public void Bot_lifecycle_events_are_public(string name)
    {
        Assert.NotNull(typeof(MezonClient).GetEvent(name, BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public async Task Bot_client_forwards_new_v216_events_and_ListFriends_overload()
    {
        var method = typeof(MezonClient).GetMethod(
            nameof(MezonClient.ListFriendsAsync),
            new[] { typeof(int?), typeof(int?), typeof(string), typeof(bool?), typeof(Mezon.Net.Core.RequestOptions) });
        Assert.NotNull(method);

        await using var bot = new MezonClient(new MezonClientOptions(1, "tok"));
        Mezon.Net.Models.SfuBreakoutRoomEventEventData? breakout = null;
        Mezon.Net.Models.FriendStateUpdatedEventData? friendState = null;

        Func<Mezon.Net.Models.SfuBreakoutRoomEventEventData, Task> breakoutHandler = payload => { breakout = payload; return Task.CompletedTask; };
        Func<Mezon.Net.Models.FriendStateUpdatedEventData, Task> friendHandler = payload => { friendState = payload; return Task.CompletedTask; };

        bot.SfuBreakoutRoomReceived += breakoutHandler;
        bot.FriendStateUpdated += friendHandler;

        var dispatch = typeof(Mezon.Net.Client.MezonClient).GetMethod("SocketMessageHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)dispatch.Invoke(bot.Engine, new object?[]
        {
            Mezon.Net.Core.MezonMessageType.Realtime,
            0,
            0,
            (ReadOnlyMemory<byte>?)null,
            new Mezon.Net.Internal.Realtime.Envelope
            {
                SfuBreakoutRoomEvent = new Mezon.Net.Internal.Realtime.SfuBreakoutRoomEvent { ChannelId = 7, Token = "tok" },
            },
        })!;
        await (Task)dispatch.Invoke(bot.Engine, new object?[]
        {
            Mezon.Net.Core.MezonMessageType.Realtime,
            0,
            0,
            (ReadOnlyMemory<byte>?)null,
            new Mezon.Net.Internal.Realtime.Envelope
            {
                FriendStateUpdated = new Mezon.Net.Internal.Realtime.FriendStateUpdated { UserId = 9, State = 1 },
            },
        })!;

        var deadline = Environment.TickCount64 + 2000;
        while ((!breakout.HasValue || !friendState.HasValue) && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(breakout.HasValue);
        Assert.True(friendState.HasValue);
        Assert.Equal((7L, "tok"), (((Mezon.Net.Models.SfuBreakoutRoomEventResponse)breakout.Value).ChannelId, ((Mezon.Net.Models.SfuBreakoutRoomEventResponse)breakout.Value).Token));
        Assert.Equal((9L, 1), (((Mezon.Net.Models.FriendStateUpdatedResponse)friendState.Value).UserId, ((Mezon.Net.Models.FriendStateUpdatedResponse)friendState.Value).State));

        bot.SfuBreakoutRoomReceived -= breakoutHandler;
        bot.FriendStateUpdated -= friendHandler;

        await Assert.ThrowsAsync<Mezon.Net.Core.MezonAuthenticationException>(() => bot.ListFriendsAsync(0, 10, null, true));
    }
}
