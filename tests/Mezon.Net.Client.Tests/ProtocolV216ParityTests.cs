using Google.Protobuf;
using Mezon.Net.Client.Models.Internal;
using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Tests;

public sealed class ProtocolV216ParityTests
{
    private static async Task<(MezonClient Client, MezonSocketClient SocketClient, LoopbackNetworkTransporter Transport)> CreateConnectedClientAsync()
    {
        var transport = new LoopbackNetworkTransporter();
        var options = new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = 60_000,
            ConnectionTimeoutInMilliseconds = 5_000,
            SocketTimeoutInMilliseconds = 2_000,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
            LogLevel = Mezon.Net.Logging.LogLevel.Error,
        };
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var client = new MezonClient(options, socketClient);
        await client.ConnectAsync();
        return (client, socketClient, transport);
    }

    [Fact]
    public async Task New_socket_apis_215_to_222_encode_requests_and_decode_responses()
    {
        var (client, socketClient, transport) = await CreateConnectedClientAsync();
        await using var _ = client;

        var calls = new List<(int Index, string Name, byte[] Body)>();
        transport.ApiResponder = req =>
        {
            calls.Add((req.ApiIndex, req.ApiName, req.Body.ToByteArray()));
            return req.ApiName switch
            {
                "SfuBreakoutRoom" => new global::Mezon.Net.Internal.Api.SfuBreakoutRoomResponse
                {
                    BreakoutId = 9001,
                    RoomCount = 2,
                    BreakRuleType = 1,
                    ListChildRooms = { 101, 102 },
                }.ToByteArray(),
                "GetOverrideEveryoneChannel" => new global::Mezon.Net.Internal.Api.GetOverrideEveryoneChannelResponse
                {
                    ClanId = 10,
                    ChannelId = 20,
                    Override = true,
                    UserIds = { 30 },
                    RoleIds = { 40 },
                }.ToByteArray(),
                "GetDMSetting" or "UpdateDMSetting" => new DMSetting { OnlyFriendsDm = true }.ToByteArray(),
                _ => Array.Empty<byte>(),
            };
        };

        var breakout = await client.SfuBreakoutRoomAsync(new SfuBreakoutRoomParams(
            parentChannelId: 500,
            roomCount: 2,
            breakRuleType: 1,
            listParticipants: [new SfuBreakoutParticipantParams(channelId: 101, userIds: [77L])]));
        Assert.Equal(9001, breakout.BreakoutId);
        Assert.Equal(2, breakout.RoomCount);
        Assert.Equal(1, breakout.BreakRuleType);
        Assert.Equal([101L, 102L], breakout.ListChildRooms);

        await client.SfuBreakoutMergeAsync(new SfuBreakoutMergeParams(targetId: 9001));

        await client.SetOverrideEveryoneChannelAsync(new SetOverrideEveryoneChannelParams(
            clanId: 10,
            channelId: 20,
            @override: true,
            excludeType: EveryoneChannelExcludeType.User,
            excludeIds: [30L]));

        var overrideInfo = await client.GetOverrideEveryoneChannelAsync(new GetOverrideEveryoneChannelParams(clanId: 10, channelId: 20));
        Assert.True(overrideInfo.Override);
        Assert.Equal([30L], overrideInfo.UserIds);
        Assert.Equal([40L], overrideInfo.RoleIds);

        var dmSetting = await client.GetDMSettingAsync();
        Assert.True(dmSetting.OnlyFriendsDm);

        var updatedDm = await client.UpdateDMSettingAsync(new UpdateDMSettingParams(onlyFriendsDm: true));
        Assert.True(updatedDm.OnlyFriendsDm);

        await client.RevokeAllSessionAsync();

        await client.RedeemProPlanCouponAsync(new RedeemProPlanCouponParams(code: "PRO-2026", clanId: 55));

        Assert.Equal(
            new[]
            {
                (215, "SfuBreakoutRoom"),
                (216, "SfuBreakoutMerge"),
                (217, "SetOverrideEveryoneChannel"),
                (218, "GetOverrideEveryoneChannel"),
                (219, "GetDMSetting"),
                (220, "UpdateDMSetting"),
                (221, "RevokeAllSession"),
                (222, "RedeemProPlanCoupon"),
            },
            calls.Select(c => (c.Index, c.Name)).ToArray());

        var breakoutReq = SfuBreakoutRoomRequest.Parser.ParseFrom(calls[0].Body);
        Assert.Equal(500, breakoutReq.ParentChannelId);
        Assert.Single(breakoutReq.ListParticipants);
        Assert.Equal(101L, breakoutReq.ListParticipants[0].ChannelId);
        Assert.Equal([77L], breakoutReq.ListParticipants[0].UserIds);

        var mergeReq = SfuBreakoutMergeRequest.Parser.ParseFrom(calls[1].Body);
        Assert.Equal(9001, mergeReq.TargetId);

        var setOverrideReq = SetOverrideEveryoneChannelRequest.Parser.ParseFrom(calls[2].Body);
        Assert.True(setOverrideReq.Override);
        Assert.Equal(EveryoneChannelExcludeType.User, setOverrideReq.ExcludeType);

        var getOverrideReq = GetOverrideEveryoneChannelRequest.Parser.ParseFrom(calls[3].Body);
        Assert.Equal((10L, 20L), (getOverrideReq.ClanId, getOverrideReq.ChannelId));

        Assert.Empty(calls[4].Body);

        var updateDmReq = UpdateDMSettingRequest.Parser.ParseFrom(calls[5].Body);
        Assert.True(updateDmReq.OnlyFriendsDm);

        Assert.Empty(calls[6].Body);

        var couponReq = RedeemProPlanCouponRequest.Parser.ParseFrom(calls[7].Body);
        Assert.Equal(("PRO-2026", 55L), (couponReq.Code, couponReq.ClanId));

        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.SfuBreakoutRoomAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.SfuBreakoutMergeAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.SetOverrideEveryoneChannelAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.GetOverrideEveryoneChannelAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.UpdateDMSettingAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socketClient.RedeemProPlanCouponAsync(null!));
    }

    [Fact]
    public async Task Rest_only_client_rejects_new_socket_apis_215_to_222()
    {
        var options = new MezonApiClientOptions();
        using var rest = new MezonApiClient(options.RestClientProvider, options);

        await Assert.ThrowsAsync<NotSupportedException>(() => rest.SfuBreakoutRoomAsync(new SfuBreakoutRoomRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.SfuBreakoutMergeAsync(new SfuBreakoutMergeRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.SetOverrideEveryoneChannelAsync(new SetOverrideEveryoneChannelRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.GetOverrideEveryoneChannelAsync(new GetOverrideEveryoneChannelRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.GetDMSettingAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.UpdateDMSettingAsync(new UpdateDMSettingRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.RevokeAllSessionAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.RedeemProPlanCouponAsync(new RedeemProPlanCouponRequest()));
    }

    [Fact]
    public async Task GenerateMeetToken_encodes_explicit_zero_current_endpoint_id()
    {
        var (client, socketClient, transport) = await CreateConnectedClientAsync();
        await using var _ = client;

        var bodies = new List<byte[]>();
        transport.ApiResponder = req =>
        {
            bodies.Add(req.Body.ToByteArray());
            return new global::Mezon.Net.Internal.Api.GenerateMeetTokenResponse
            {
                Token = "eyJ.tok.sig",
                Url = "wss://sfu.example",
            }.ToByteArray();
        };

        await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId: 123, roomName: "r", metadata: "m"));
        await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId: 123, roomName: "r", metadata: "m", currentEndpointId: 0));
        await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId: 123, roomName: "r", metadata: "m", currentEndpointId: 3));
        await client.GenerateMeetTokenAsync(new GenerateMeetTokenParams(channelId: 123, roomName: "r", metadata: "m", currentEndpointId: -1));
        await socketClient.GenerateMeetTokenAsync(new GenerateMeetTokenRequest { ChannelId = 123, RoomName = "r", Metadata = "m" });

        var omittedHex = Convert.ToHexString(bodies[0]).ToLowerInvariant();
        var zeroHex = Convert.ToHexString(bodies[1]).ToLowerInvariant();
        var threeHex = Convert.ToHexString(bodies[2]).ToLowerInvariant();
        var negHex = Convert.ToHexString(bodies[3]).ToLowerInvariant();
        var directHex = Convert.ToHexString(bodies[4]).ToLowerInvariant();

        Assert.DoesNotContain("2000", omittedHex);
        Assert.Equal(omittedHex, directHex);
        Assert.Equal(omittedHex + "2000", zeroHex);
        Assert.Equal(omittedHex + "2003", threeHex);
        Assert.Equal(omittedHex + "20ffffffffffffffffff01", negHex);

        Assert.Equal(0, GenerateMeetTokenRequest.Parser.ParseFrom(bodies[1]).CurrentEndpointId);
        Assert.Equal(3, GenerateMeetTokenRequest.Parser.ParseFrom(bodies[2]).CurrentEndpointId);
        Assert.Equal(-1, GenerateMeetTokenRequest.Parser.ParseFrom(bodies[3]).CurrentEndpointId);

        var directZero = MezonSocketClient.SerializeGenerateMeetTokenRequest(
            new GenerateMeetTokenRequest { ChannelId = 1, CurrentEndpointId = 2 },
            includeZeroCurrentEndpointId: true);
        Assert.Equal(2, GenerateMeetTokenRequest.Parser.ParseFrom(directZero).CurrentEndpointId);

        var directNonZeroFlagOff = MezonSocketClient.SerializeGenerateMeetTokenRequest(
            new GenerateMeetTokenRequest { ChannelId = 1, CurrentEndpointId = 0 },
            includeZeroCurrentEndpointId: false);
        Assert.Equal("0801", Convert.ToHexString(directNonZeroFlagOff.ToByteArray()).ToLowerInvariant());
    }

    [Fact]
    public async Task ListFriends_and_ListChannelMessages_forward_new_optional_fields()
    {
        var (client, _, transport) = await CreateConnectedClientAsync();
        await using var _ = client;

        byte[]? lastBody = null;
        transport.ApiResponder = req =>
        {
            lastBody = req.Body.ToByteArray();
            return req.ApiName switch
            {
                "ListFriends" => new FriendList().ToByteArray(),
                "ListChannelMessages" => new ChannelMessageList().ToByteArray(),
                _ => Array.Empty<byte>(),
            };
        };

        await client.ListFriendsAsync(state: 0, limit: 25, cursor: "c1", sortByLastSent: true);
        var friendsReq = ListFriendsRequest.Parser.ParseFrom(lastBody!);
        Assert.Equal((0, 25, "c1", true), (friendsReq.State, friendsReq.Limit, friendsReq.Cursor, friendsReq.SortByLastSent));

        await client.ListFriendsAsync(state: null, limit: null, cursor: null);
        var friendsDefault = ListFriendsRequest.Parser.ParseFrom(lastBody!);
        Assert.False(friendsDefault.SortByLastSent);

        await client.ListChannelMessagesAsync(clanId: 1, channelId: 2, messageId: 3, direction: 1, limit: 10, topicId: 4, lastSeenMessageId: 999);
        var msgsReq = ListChannelMessagesRequest.Parser.ParseFrom(lastBody!);
        Assert.Equal((1L, 2L, 3L, 1, 10, 4L, 999L), (msgsReq.ClanId, msgsReq.ChannelId, msgsReq.MessageId, msgsReq.Direction, msgsReq.Limit, msgsReq.TopicId, msgsReq.LastSeenMessageId));

        await client.ListChannelMessagesAsync(clanId: 1, channelId: 2, messageId: null, direction: null, limit: null, topicId: null);
        var msgsDefault = ListChannelMessagesRequest.Parser.ParseFrom(lastBody!);
        Assert.Equal(0L, msgsDefault.LastSeenMessageId);
    }

    [Fact]
    public void Event_IsPrivate_obsolete_aliases_and_v216_fields_work()
    {
#pragma warning disable CS0618
        var fromLegacyParam = new CreateEventParams(title: "Standup", isPrivate: true, coHost: 88);
        Assert.True(fromLegacyParam.IsExternal);
        Assert.True(fromLegacyParam.IsPrivate);
        Assert.Equal(88L, fromLegacyParam.CoHost);
        var legacyProto = CreateEventParamsMapper.ToProto(fromLegacyParam);
        Assert.True(legacyProto.IsExternal);
        Assert.Equal(88L, legacyProto.CoHost);

        var fromNewParam = new CreateEventParams(title: "External", isExternal: false, isPrivate: true);
        Assert.False(fromNewParam.IsExternal);
        Assert.False(fromNewParam.IsPrivate);

        var evtResp = new EventManagementResponse(new EventManagement { IsExternal = true, CoHost = 99 });
        Assert.True(evtResp.IsExternal);
        Assert.True(evtResp.IsPrivate);
        Assert.Equal(99L, evtResp.CoHost);
#pragma warning restore CS0618

        var updateEvtProto = UpdateEventParamsMapper.ToProto(new UpdateEventParams(coHost: 123));
        Assert.Equal(123L, updateEvtProto.CoHost);

        var updateAccountProto = UpdateAccountParamsMapper.ToProto(new UpdateAccountParams(banner: "https://cdn/b.png", proPlan: true));
        Assert.Equal("https://cdn/b.png", updateAccountProto.Banner);
        Assert.True(updateAccountProto.ProPlan);

        var updateClanProto = UpdateClanDescParamsMapper.ToProto(new UpdateClanDescParams(proPlan: true));
        Assert.True(updateClanProto.ProPlan);

        var accountResp = new AccountResponse(new Account
        {
            Banner = "https://cdn/a.png",
            ProPlan = true,
            User = new User { Banner = "https://cdn/u.png", ProPlan = true },
        });
        Assert.Equal(("https://cdn/a.png", true), (accountResp.Banner, accountResp.ProPlan));
        Assert.Equal(("https://cdn/u.png", true), (accountResp.User.Banner, accountResp.User.ProPlan));

        var clanResp = new ClanDescResponse(new ClanDesc { ProPlan = true, TotalMembers = 420 });
        Assert.Equal((true, 420), (clanResp.ProPlan, clanResp.TotalMembers));

        var addFriendsResp = new Mezon.Net.Models.AddFriendsResponse(new global::Mezon.Net.Internal.Api.AddFriendsResponse
        {
            DisplayNames = { "Alice" },
            Avatars = { "https://cdn/alice.png" },
        });
        Assert.Equal(["Alice"], addFriendsResp.DisplayNames);
        Assert.Equal(["https://cdn/alice.png"], addFriendsResp.Avatars);

        var addFriendRt = new AddFriend { UserId = 5, Accepted = true };
        Assert.True(addFriendRt.Accepted);
    }
}
