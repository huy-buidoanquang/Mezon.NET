using System.Text.Json.Nodes;
using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

/// <summary>The real Room, sessions and negotiation against a scripted server on a real WebSocket, with a fake PeerConnection (no media).</summary>
public class IntegrationTests
{
    private static object SlotAssigned(uint slot, long generation, uint peer, bool audio, bool video) => new
    {
        type = "slot_assigned",
        slot,
        generation,
        peer_id = peer,
        user_id = (9000 + peer).ToString(),
        audio_active = audio,
        video_active = video,
        screen_active = false,
    };

    [Fact]
    public async Task Connect_sends_the_initial_mute_before_answering()
    {
        await using var h = Harness.Create();
        await h.Connect();
        Assert.Equal(ConnectionState.Connected, h.Room.State);
        Assert.Equal("7", h.Room.LocalParticipant.UserId);
        Assert.Equal(100u, h.Room.LocalParticipant.PeerId);
        Assert.Equal("42", h.Room.RoomId);
        var c = h.Sfu.Current;
        Assert.Equal(new[] { "join", "mute", "answer" }, c.Types().Take(3));
        var mute = c.Messages("mute")[0];
        Assert.Equal((true, 1L), (mute["is_mute"]!.GetValue<bool>(), mute["mute_revision"]!.GetValue<long>()));
        var join = c.Messages("join")[0];
        Assert.Equal(("speaker", "vp8"), (join["role"]!.GetValue<string>(), join["screen_codec"]!.GetValue<string>()));
        await h.WaitEvent("state:Connected");
    }

    [Fact]
    public async Task The_token_is_never_put_in_the_url()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var line = Assert.Single(h.Sfu.RequestLines);
        Assert.StartsWith("GET /ws HTTP/1.1", line);
    }

    [Theory]
    [InlineData(IceServersPolicy.FromServer, "stun:stun.example:3478")]
    [InlineData(IceServersPolicy.Merge, "stun:stun.example:3478,turn:own.example:3478")]
    [InlineData(IceServersPolicy.Override, "turn:own.example:3478")]
    public async Task Ice_server_policies(IceServersPolicy policy, string want)
    {
        await using var h = Harness.Create(o => o with
        {
            IceServersPolicy = policy,
            IceServers = policy == IceServersPolicy.FromServer ? Array.Empty<IceServer>() : new[] { new IceServer(new[] { "turn:own.example:3478" }) },
        });
        await h.Connect();
        Assert.Equal(want, string.Join(",", h.Sfu.Peer.Config.IceServers.SelectMany(s => s.Urls)));
    }

    [Fact]
    public async Task Cancelling_connect_leaves_the_room_disconnected()
    {
        await using var h = Harness.Create();
        h.Sfu.SilentJoin = true;
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Connect(cancel.Token));
        Assert.Equal(ConnectionState.Disconnected, h.Room.State);
        await h.WaitEvent("disconnected:Client");
        var first = h.Sfu.Current;
        await h.Wait("the socket to close", () => first.ClientClose is not null || first.Ended);
        h.Sfu.SilentJoin = false;
        await h.Connect();
        Assert.Equal(ConnectionState.Connected, h.Room.State);
    }

    private static void CloseRightAfterTheFirstSnapshot(FakeSfu sfu) => sfu.On("answer", (conn, _) =>
    {
        if (sfu.ConnCount > 1)
        {
            return false;
        }

        sfu.Snapshot(conn);
        conn.Close(1001);
        return true;
    });

    [Fact]
    public async Task A_session_closed_right_after_admission_is_retried()
    {
        // The probe holds the connecting task until the admitted session has ended: it must count as a failed
        // attempt, not leave the room Connected without a session.
        FakeSfu? sfu = null;
        await using var h = Harness.Create(o => o with
        {
            AdmissionProbe = async () =>
            {
                while (sfu is null || sfu.ConnCount < 1 || !sfu.First.Ended)
                {
                    await Task.Delay(5);
                }
            },
        });
        sfu = h.Sfu;
        CloseRightAfterTheFirstSnapshot(h.Sfu);
        await h.Connect();
        Assert.Equal(2, h.Sfu.ConnCount);
        Assert.Equal(ConnectionState.Connected, h.Room.State);
        Assert.False(h.Has("reconnecting"));
        await h.Room.SendMessageAsync("hi");
    }

    [Fact]
    public async Task A_session_closed_around_admission_ends_connected_with_a_live_session()
    {
        // Without the probe either order can win: admitted first (then a normal reconnect) or ended first (a retry).
        await using var h = Harness.Create();
        CloseRightAfterTheFirstSnapshot(h.Sfu);
        await h.Connect();
        await h.Wait("the second session", () => h.Sfu.ConnCount == 2 && h.Room.State == ConnectionState.Connected);
        await h.Room.SendMessageAsync("hi");
    }

    [Fact]
    public async Task A_failed_first_attempt_leaves_no_slot_state_behind()
    {
        await using var h = Harness.Create();
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Fixed(0) };
        h.Sfu.Members = new List<object> { FakeSfu.Member(5), FakeSfu.Member(6) };
        object Slot(uint peer, long generation) => new
        {
            type = "slot_assigned",
            slot = 0,
            generation,
            peer_id = peer,
            user_id = (9000 + peer).ToString(),
            mid_audio = 3,
            mid_video = 4,
            mid_screen = 5,
            audio_active = true,
            video_active = false,
            screen_active = false,
        };
        h.Sfu.On("answer", (conn, _) =>
        {
            if (conn.Answered > 0)
            {
                return false;
            }

            conn.Answered = 1;
            if (h.Sfu.ConnCount == 1)
            {
                conn.Send(Slot(6, 1));
                conn.Send(Slot(5, 2));
                conn.Close(4014);
            }
            else
            {
                h.Sfu.Snapshot(conn);
                conn.Send(Slot(6, 1));
            }

            return true;
        });
        await h.Connect();
        Assert.Equal(2, h.Sfu.ConnCount);
        await h.WaitEvent("sub:3:Microphone:6");
        Assert.False(h.Has("sub:3:Microphone:5"));
    }

    [Fact]
    public async Task Unbounded_timeouts_are_accepted()
    {
        await using var h = Harness.Create(o => o with
        {
            Timeouts = new Timeouts
            {
                Join = TimeSpan.MaxValue,
                Admission = Timeout.InfiniteTimeSpan,
                Operation = TimeSpan.MaxValue,
                InboundIdle = TimeSpan.MaxValue,
                Token = TimeSpan.MaxValue,
                Dial = Timeout.InfiniteTimeSpan,
            },
            Reconnect = new ReconnectOptions { MaxDuration = TimeSpan.MaxValue, Delay = _ => TimeSpan.FromMilliseconds(10) },
        });
        await h.Connect();
        await h.Room.SendMessageAsync("hi");
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
    }

    [Fact]
    public async Task Join_timeout_ends_with_join_failed()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Join = TimeSpan.FromMilliseconds(200) } });
        h.Sfu.SilentJoin = true;
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        Assert.Equal((SfuErrorKind.Timeout, "join_timeout"), (error.Kind, error.Code));
        await h.WaitEvent("disconnected:JoinFailed");
        Assert.Equal(3, h.Sfu.ConnCount);
    }

    private static void RejectJoins(FakeSfu sfu, int times)
    {
        var count = 0;
        sfu.On("join", (c, _) =>
        {
            if (Interlocked.Increment(ref count) > times)
            {
                return false;
            }

            c.Send(new { type = "error", message = "invalid_token" });
            c.Close(4005);
            return true;
        });
    }

    [Fact]
    public async Task A_rejected_fixed_token_ends_with_auth_failed()
    {
        await using var h = Harness.Create();
        RejectJoins(h.Sfu, 100);
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        Assert.Equal(SfuErrorKind.Authentication, error.Kind);
        await h.WaitEvent("disconnected:AuthFailed");
    }

    [Fact]
    public async Task The_provider_is_asked_again_with_the_rejected_token()
    {
        await using var h = Harness.Create();
        RejectJoins(h.Sfu, 1);
        var requests = new List<(TokenReason, string?)>();
        var tokens = new Queue<string>(new[] { FakeSfu.Token("7", 3600), FakeSfu.Token("7", 3601) });
        var provider = TokenProvider.Create((r, _) =>
        {
            lock (requests)
            {
                requests.Add((r.Reason, r.Previous));
                return Task.FromResult(tokens.Dequeue());
            }
        });
        var first = tokens.Peek();
        await h.Room.ConnectAsync(h.Sfu.Url, provider);
        Assert.Equal(new (TokenReason, string?)[] { (TokenReason.Initial, null), (TokenReason.Rejected, first) }, requests);
    }

    [Fact]
    public async Task A_provider_failure_during_reconnect_is_retried()
    {
        await using var h = Harness.Create();
        var calls = 0;
        var provider = TokenProvider.Create((_, _) => Interlocked.Increment(ref calls) switch
        {
            1 => Task.FromResult(FakeSfu.Token("7", 30)),
            2 => Task.FromException<string>(new IOException("network unreachable")),
            _ => Task.FromResult(FakeSfu.Token("7", 3600)),
        });
        await h.Room.ConnectAsync(h.Sfu.Url, provider);
        await h.WaitEvent("state:Connected");
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        Assert.Equal(3, Volatile.Read(ref calls));
        Assert.False(h.Has("disconnected:AuthFailed"));
    }

    [Fact]
    public async Task A_hanging_provider_times_out()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Token = TimeSpan.FromMilliseconds(100) } });
        var provider = TokenProvider.Create((_, _) => new TaskCompletionSource<string>().Task);
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.ConnectAsync(h.Sfu.Url, provider));
        Assert.Equal((SfuErrorKind.Authentication, "token_provider_failed"), (error.Kind, error.Code));
        await h.WaitEvent("disconnected:AuthFailed");
    }

    [Fact]
    public async Task A_hanging_provider_during_reconnect_is_retried_after_its_timeout()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Token = TimeSpan.FromMilliseconds(100) } });
        var calls = 0;
        var provider = TokenProvider.Create((_, _) => Interlocked.Increment(ref calls) switch
        {
            1 => Task.FromResult(FakeSfu.Token("7", 30)),
            2 => new TaskCompletionSource<string>().Task,
            _ => Task.FromResult(FakeSfu.Token("7", 3600)),
        });
        await h.Room.ConnectAsync(h.Sfu.Url, provider);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        Assert.Equal(3, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Disposing_the_room_closes_the_peer_connection_the_socket_and_its_tasks()
    {
        var h = Harness.Create();
        await h.Connect();
        await h.WaitEvent("state:Connected");
        var peer = h.Sfu.Peer;
        var conn = h.Sfu.Current;
        await h.Room.DisposeAsync();
        Assert.True(peer.Closed, "PeerConnection left open");
        Assert.Equal(1000, conn.ClientClose);
        await h.Wait("the socket to close", () => conn.Ended);
        await h.WaitEvent("disconnected:Client");
        var connections = h.Sfu.ConnCount;
        await Task.Delay(300);
        Assert.Equal(connections, h.Sfu.ConnCount);
        Assert.Equal(ConnectionState.Disconnected, h.Room.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => h.Connect());
        Assert.True(h.Room.Events.Completion.IsCompleted, "the event stream was not completed");
        await h.DisposeAsync();
    }

    [Fact]
    public async Task Disconnect_closes_normally_and_can_connect_again()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var first = h.Sfu.Current;
        h.Sfu.On("send_message", (_, _) => true);
        var pending = h.Room.SendMessageAsync("pending");
        await h.Wait("message sent", () => first.Messages("send_message").Count == 1);
        await h.Room.DisconnectAsync();
        var error = await Assert.ThrowsAsync<SfuException>(() => pending);
        Assert.Equal((SfuErrorKind.Disconnected, "client"), (error.Kind, error.Code));
        await h.Wait("client close frame", () => first.ClientClose == 1000);
        await h.WaitEvent("disconnected:Client");
        Assert.Equal(ConnectionState.Disconnected, h.Room.State);
        Assert.Equal(0, h.Count("reconnecting"));
        await h.Connect();
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Answers_each_offer_and_ignores_stale_ones()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var c = h.Sfu.Current;
        c.Offer(new List<FixtureSlot>());
        await h.Wait("two answers", () => c.Answers().Count == 2);
        c.Offer(new List<FixtureSlot>());
        await h.Wait("three answers", () => c.Answers().Count == 3);
        Assert.Equal(new long[] { 0, 1, 2 }, c.Answers());
        c.Offer(new List<FixtureSlot>(), 1);
        await Task.Delay(100);
        Assert.Equal(3, c.Answers().Count);
    }

    [Fact]
    public async Task Repeated_negotiation_failures_replace_the_session()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.FailOffers = 3;
        var c = h.Sfu.Current;
        for (var i = 0; i < 3; i++)
        {
            c.Offer(new List<FixtureSlot>());
            await Task.Delay(50);
        }

        await h.WaitEvent("reconnected");
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Snapshot_participants_and_dynamic_tracks()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2), FakeSfu.MemberWith(3, m => m["camera_active"] = true) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Peer("9002", 2) with { NoCamera = true }, FixtureSlot.Peer("9003", 3) };
        await h.Connect();
        await h.WaitEvent("sub:6:Microphone:3");
        Assert.True(h.Has("sub:3:Microphone:2") && h.Has("sub:7:Camera:3") && h.Has("mode:Dynamic"));
        Assert.Equal(2, h.Room.RemoteParticipants.Count);
        var p = h.Room.GetParticipant(2)!;
        Assert.Equal("Peer2", p.Info.DisplayName);
        Assert.True(p.AudioTrack is not null && p.CameraTrack is null);
        Assert.Equal((DownlinkMode.Dynamic, 3), (h.Room.DownlinkMode, h.Room.ParticipantCount));

        h.Sfu.Peer.Deliver(3, 1);
        h.Sfu.Peer.Deliver(3, 2);
        var track = p.AudioTrack!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        for (ushort want = 1; want <= 2; want++)
        {
            Assert.Equal(want, (await track.ReadRtpAsync(timeout.Token))!.SequenceNumber);
        }

        Assert.Equal("audio/opus", track.MimeType);
        Assert.Equal(2, track.Stats.PacketsReceived);
    }

    [Fact]
    public async Task A_slow_reader_loses_the_oldest_packets()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Peer("9002", 2) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        for (var i = 0; i < RemoteTrack.BufferSize + 10; i++)
        {
            h.Sfu.Peer.Deliver(3, (ushort)i);
        }

        var track = h.Room.GetParticipant(2)!.AudioTrack!;
        Assert.Equal(10, track.Stats.PacketsDropped);
        Assert.Equal((ushort)10, (await track.ReadRtpAsync())!.SequenceNumber);
    }

    [Fact]
    public async Task Dynamic_camera_follows_camera_active()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Peer("9002", 2) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        Assert.False(h.Has("sub:4:Camera:2"));
        var c = h.Sfu.Current;
        c.Send(new { type = "peer_updated", peer = FakeSfu.MemberWith(2, m => { m["camera_requested"] = true; m["camera_active"] = true; }) });
        await h.WaitEvent("sub:4:Camera:2");
        c.Send(new { type = "peer_updated", peer = FakeSfu.Member(2) });
        await h.WaitEvent("unsub:4:Camera:2");
    }

    [Fact]
    public async Task A_reused_mid_is_reattributed()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Peer("9002", 2) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        var old = h.Room.GetParticipant(2)!.AudioTrack!;
        var c = h.Sfu.Current;
        c.Send(new { type = "peer_left", room_revision = 2, participant_count = 1, peer_id = 2, user_id = "9002", slot = 4294967295u });
        c.Send(new { type = "peer_joined", room_revision = 3, participant_count = 2, peer = FakeSfu.Member(5) });
        c.Offer(new List<FixtureSlot> { FixtureSlot.Peer("9005", 5) });
        await h.WaitEvent("sub:3:Microphone:5");
        Assert.True(h.Has("unsub:3:Microphone:2") && h.Has("left:2") && old.IsEnded);
        Assert.Null(await old.ReadRtpAsync());
        h.Sfu.Peer.Deliver(3, 9);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal((ushort)9, (await h.Room.GetParticipant(5)!.AudioTrack!.ReadRtpAsync(timeout.Token))!.SequenceNumber);
    }

    [Fact]
    public async Task Membership_deltas_are_applied_in_order()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var c = h.Sfu.Current;
        c.Send(new { type = "peer_updated", peer = FakeSfu.MemberWith(4, m => m["is_mute"] = true) });
        c.Send(new { type = "peer_joined", room_revision = 2, participant_count = 2, peer = FakeSfu.Member(4) });
        await h.WaitEvent("joined:4");
        Assert.True(h.Room.GetParticipant(4)!.IsMuted, "orphan update not applied on join");
        c.Send(new { type = "peer_joined", room_revision = 2, participant_count = 3, peer = FakeSfu.Member(5) });
        c.Send(new { type = "peer_left", room_revision = 3, participant_count = 1, peer_id = 4, user_id = "9004", slot = 4294967295u });
        await h.WaitEvent("left:4");
        Assert.False(h.Has("joined:5"), "applied a stale peer_joined");
        c.Send(new { type = "peer_updated", peer = FakeSfu.Member(4) });
        await Task.Delay(50);
        Assert.Null(h.Room.GetParticipant(4));
    }

    [Fact]
    public async Task Fixed_slots_follow_slot_events()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2), FakeSfu.Member(3) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Fixed(0), FixtureSlot.Fixed(1) };
        await h.Connect();
        await h.WaitEvent("mode:Fixed");
        var c = h.Sfu.Current;
        c.Send(SlotAssigned(0, 1, 2, true, false));
        await h.WaitEvent("sub:3:Microphone:2");
        c.Send(SlotAssigned(0, 1, 2, false, true));
        await h.WaitEvent("muted:3");
        await h.WaitEvent("sub:4:Camera:2");
        c.Send(SlotAssigned(0, 2, 3, true, false));
        await h.WaitEvent("sub:3:Microphone:3");
        Assert.True(h.Has("unsub:3:Microphone:2") && h.Has("unsub:4:Camera:2"));
        c.Send(SlotAssigned(0, 1, 2, true, false));
        c.Send(new { type = "slot_released", slot = 0, generation = 1 });
        await Task.Delay(50);
        Assert.NotNull(h.Room.GetParticipant(3)!.AudioTrack);
        Assert.Equal(new uint[] { 3 }, h.Room.ActiveSpeakers.Select(p => p.PeerId));
        c.Send(new { type = "slot_released", slot = 0, generation = 2 });
        await h.WaitEvent("unsub:3:Microphone:3");
        Assert.Empty(h.Room.ActiveSpeakers);
    }

    [Fact]
    public async Task Speakers_are_cleared_when_the_session_ends()
    {
        await using var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(60) } });
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Fixed(0) };
        await h.Connect();
        await h.WaitEvent("mode:Fixed");
        h.Sfu.Current.Send(SlotAssigned(0, 1, 2, true, false));
        await h.Wait("a speaker", () => h.Room.ActiveSpeakers.Count == 1);
        var speaker = h.Room.GetParticipant(2)!;
        Assert.True(speaker.IsSpeaking);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnecting");
        Assert.Empty(h.Room.ActiveSpeakers);
        Assert.False(speaker.IsSpeaking);
        await h.Room.DisconnectAsync();
        Assert.Empty(h.Room.ActiveSpeakers);
    }

    [Fact]
    public async Task Room_messages_carry_the_sender()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        await h.Connect();
        var c = h.Sfu.Current;
        c.Send(new { type = "room_message", message = "hi", user_id = "9002", peer_id = 2 });
        c.Send(new { type = "room_message", message = "who", user_id = "5", peer_id = 77 });
        await h.Wait("messages", () => h.Messages.Count == 2);
        Assert.Equal(new[] { "9002/2/True: hi", "5/77/False: who" }, h.Messages);
    }

    [Fact]
    public async Task Frames_with_raw_control_characters_are_not_dropped()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        await h.Connect();
        var c = h.Sfu.Current;
        c.SendRaw("{\"type\":\"room_message\",\"message\":\"he\u0001llo\",\"user_id\":\"9002\",\"peer_id\":2}");
        c.SendRaw("{\"type\":\"peer_updated\",\"peer\":{\"peer_id\":2,\"user_id\":\"9002\",\"metadata\":\"Re\u0007named;\",\"role\":\"speaker\"}}");
        await h.Wait("the message", () => h.Messages.Count == 1);
        Assert.Equal("9002/2/True: hello", h.Messages[0]);
        await h.Wait("the rename", () => h.Room.GetParticipant(2)?.Info.DisplayName == "Renamed");
    }

    [Fact]
    public async Task A_frame_with_invalid_utf8_ends_the_session_and_the_room_reconnects()
    {
        // .NET's WebSocket validates UTF-8 in text frames and fails the read (like the Rust SDK's tungstenite).
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        await h.Connect();
        var frame = new List<byte>(System.Text.Encoding.ASCII.GetBytes("{\"type\":\"room_message\",\"message\":\"a"));
        frame.AddRange(new byte[] { 0xC3, 0x28 });
        frame.AddRange(System.Text.Encoding.ASCII.GetBytes("\",\"user_id\":\"9002\",\"peer_id\":2}"));
        h.Sfu.Current.SendTextBytes(frame.ToArray());
        await h.WaitEvent("reconnected");
        Assert.Empty(h.Messages);
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task The_microphone_uses_increasing_revisions()
    {
        await using var h = Harness.Create();
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        await h.Connect();
        var c = h.Sfu.Current;
        Assert.False(c.Messages("mute")[0]["is_mute"]!.GetValue<bool>());
        Assert.True(h.Room.LocalParticipant.Microphone.IsEnabled);
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(false);
        var last = c.Messages("mute").Last();
        Assert.Equal((true, 2L), (last["is_mute"]!.GetValue<bool>(), last["mute_revision"]!.GetValue<long>()));
        Assert.False(h.Room.LocalParticipant.Microphone.IsEnabled);
    }

    [Fact]
    public async Task Opus_frames_carry_timestamps_markers_and_levels()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var mic = h.Room.LocalParticipant.Microphone;
        Assert.False(mic.WriteOpus(new byte[] { 0xF8, 1 }, TimeSpan.FromMilliseconds(20), 30));
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        Assert.True(mic.WriteOpus(new byte[] { 0xF8, 1 }, TimeSpan.FromMilliseconds(20), 30));
        Assert.True(mic.WriteOpus(new byte[] { 0xF8, 2 }, TimeSpan.FromMilliseconds(20), 127));
        Assert.True(mic.WriteOpus(new byte[] { 0xF8, 3 }, TimeSpan.FromMilliseconds(20)));
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(false);
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        Assert.True(mic.WriteOpus(new byte[] { 0xF8, 4 }, TimeSpan.FromMilliseconds(20), 200));
        var sent = h.Sfu.Peer.Microphone.Sent;
        Assert.Equal(4, sent.Count);
        Assert.Equal(new[] { true, false, false, true }, sent.Select(s => s.Marker));
        Assert.Equal(sent[0].Timestamp + 960, sent[1].Timestamp);
        Assert.Equal(sent[0].Timestamp + 2880, sent[3].Timestamp);
        Assert.Equal(new (byte, bool)[] { (30, true), (127, false), (0, true), (127, false) }, sent.Select(s => (s.Level, s.Voice)));
        Assert.Equal(4, mic.FramesSent);
    }

    [Fact]
    public async Task A_stale_mute_revision_is_reported()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.On("mute", (c, _) =>
        {
            c.Send(new { type = "error", message = "stale_mute_revision" });
            return true;
        });
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true));
        Assert.Equal((SfuErrorKind.Signaling, "stale_mute_revision", "mute"), (error.Kind, error.Code, error.Operation));
    }

    [Fact]
    public async Task The_camera_needs_no_renegotiation()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var c = h.Sfu.Current;
        var answers = c.Answers().Count;
        await h.Room.LocalParticipant.SetCameraEnabledAsync(true);
        Assert.True(h.Room.LocalParticipant.Camera.IsEnabled);
        Assert.True(c.Messages("camera")[0]["active"]!.GetValue<bool>());
        Assert.True(h.Room.LocalParticipant.Camera.WriteVp8(new byte[] { 0x10, 0x02, 0x00 }, TimeSpan.FromMilliseconds(33)));
        Assert.Equal(1, h.Sfu.Peer.Camera.Frames);
        await h.Room.LocalParticipant.SetCameraEnabledAsync(false);
        Assert.False(h.Room.LocalParticipant.Camera.IsEnabled);
        Assert.False(h.Room.LocalParticipant.Camera.WriteVp8(new byte[] { 0x10 }, TimeSpan.FromMilliseconds(33)));
        Assert.Equal(answers, c.Answers().Count);
    }

    [Fact]
    public async Task Audience_controls()
    {
        await using var h = Harness.Create(o => o with { Role = Role.Audience });
        await h.Connect();
        var c = h.Sfu.Current;
        Assert.Equal("audience", c.Messages("join")[0]["role"]!.GetValue<string>());
        Assert.False(h.Sfu.Peer.Config.Camera);
        Assert.Equal(SfuErrorKind.State, (await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetCameraEnabledAsync(true))).Kind);
        Assert.Equal(SfuErrorKind.State, (await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true))).Kind);
        await h.Room.LocalParticipant.SetPushToTalkAsync(true);
        Assert.True(h.Room.LocalParticipant.Microphone.IsEnabled);
        Assert.False(c.Messages("mute").Last()["is_mute"]!.GetValue<bool>());
        Assert.True(c.Messages("push_to_talk")[0]["active"]!.GetValue<bool>());
        await h.Room.LocalParticipant.SetPushToTalkAsync(false);
        Assert.False(h.Room.LocalParticipant.Microphone.IsEnabled);

        await using var speaker = Harness.Create();
        await speaker.Connect();
        Assert.Equal(SfuErrorKind.State, (await Assert.ThrowsAsync<SfuException>(() => speaker.Room.LocalParticipant.SetPushToTalkAsync(true))).Kind);
    }

    [Fact]
    public async Task Control_requests()
    {
        await using var h = Harness.Create(o => o with
        {
            ModerationTokens = ModerationTokenProvider.Create((action, user, _) => Task.FromResult(FakeSfu.Token(user, 60))),
        });
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        await h.Connect();
        var c = h.Sfu.Current;
        await h.Room.LocalParticipant.SetMetadataAsync("New;");
        Assert.Equal("New;", c.Messages("peer_update")[0]["metadata"]!.GetValue<string>());
        // The acknowledgement is matched against the sanitized text that was actually sent.
        await h.Room.LocalParticipant.SetMetadataAsync("Ne\u0001w2;");
        Assert.Equal("New2;", c.Messages("peer_update")[1]["metadata"]!.GetValue<string>());
        Assert.Equal(1, await h.Room.MuteParticipantAsync("9002"));
        await h.Room.RequestKeyframeAsync(h.Room.GetParticipant(2)!, TrackSource.Screen);
        var keyframe = c.Messages("request_keyframe")[0];
        Assert.Equal(("screen", 2L), (keyframe["kind"]!.GetValue<string>(), keyframe["publisher_id"]!.GetValue<long>()));
        Assert.Equal(new VideoReceiving(false, true), await h.Room.SetVideoReceivingAsync(new VideoReceiving(false, true)));
        Assert.Equal(SfuErrorKind.Protocol, (await Assert.ThrowsAsync<SfuException>(() => h.Room.SendMessageAsync(""))).Kind);
        await using var plain = Harness.Create();
        Assert.Equal(SfuErrorKind.State, (await Assert.ThrowsAsync<SfuException>(() => plain.Room.KickParticipantAsync("1"))).Kind);
        Assert.Equal(SfuErrorKind.NotConnected, (await Assert.ThrowsAsync<SfuException>(() => plain.Room.SendMessageAsync("x"))).Kind);
    }

    [Fact]
    public async Task Errors_are_routed_to_the_right_request()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.On("send_message", (c, _) =>
        {
            c.Send(new { type = "error", message = "role_change_disabled" });
            c.Send(new { type = "error", message = "invalid_message" });
            return true;
        });
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.SendMessageAsync("x"));
        Assert.Equal("invalid_message", error.Code);
        await h.WaitEvent("signal-error:role_change_disabled");
    }

    [Fact]
    public async Task An_unacknowledged_request_times_out()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Operation = TimeSpan.FromMilliseconds(300) } });
        await h.Connect();
        h.Sfu.On("send_message", (_, _) => true);
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.SendMessageAsync("x"));
        Assert.Equal((SfuErrorKind.Timeout, "send_message"), (error.Kind, error.Code));
    }

    [Fact]
    public async Task A_cancelled_request_throws_operation_cancelled()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.On("send_message", (_, _) => true);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Room.SendMessageAsync("x", cancel.Token));
    }

    [Fact]
    public async Task A_moderator_mute_is_detected()
    {
        await using var h = Harness.Create();
        await h.Connect();
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        var c = h.Sfu.Current;
        c.IsMute = true;
        c.Send(new { type = "peer_updated", peer = c.Own() });
        await h.WaitEvent("forced-mute");
        Assert.False(h.Room.LocalParticipant.IsMicrophoneEnabled);
        Assert.False(h.Room.LocalParticipant.Microphone.IsEnabled);
        await h.Wait("mute after the forced mute", () => c.Messages("mute").LastOrDefault()?["is_mute"]?.GetValue<bool>() == true);
    }

    [Fact]
    public async Task A_moderator_mute_is_detected_after_a_mute_timed_out()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var ignored = 0;
        h.Sfu.On("mute", (_, _) => Interlocked.Exchange(ref ignored, 1) == 0);
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true));
        Assert.Equal(SfuErrorKind.Timeout, error.Kind);
        var c = h.Sfu.Current;
        c.IsMute = true;
        c.Send(new { type = "peer_updated", peer = c.Own() });
        await h.WaitEvent("forced-mute");
    }

    [Fact]
    public async Task A_moderator_mute_is_detected_after_a_mute_error()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var rejected = 0;
        h.Sfu.On("mute", (c, _) =>
        {
            if (Interlocked.Exchange(ref rejected, 1) != 0)
            {
                return false;
            }

            c.Send(new { type = "error", message = "session_not_found" });
            return true;
        });
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true));
        Assert.Equal("session_not_found", error.Code);
        var conn = h.Sfu.Current;
        conn.IsMute = true;
        conn.Send(new { type = "peer_updated", peer = conn.Own() });
        await h.WaitEvent("forced-mute");
    }

    [Fact]
    public async Task Room_capacity_ends_with_room_full()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.Current.Send(new { type = "error", message = "room_capacity" });
        await h.WaitEvent("disconnected:RoomFull");
    }

    [Fact]
    public async Task Room_capacity_on_the_first_connect_is_not_retried()
    {
        await using var h = Harness.Create();
        h.Sfu.On("join", (conn, _) =>
        {
            conn.Send(new { type = "error", message = "room_capacity" });
            return true;
        });
        await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        await h.WaitEvent("disconnected:RoomFull");
        Assert.Equal(1, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task A_url_that_is_not_websocket_fails_at_once()
    {
        await using var h = Harness.Create();
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.ConnectAsync(h.Sfu.Url.Replace("ws://", "http://"), TokenProvider.Fixed(FakeSfu.Token("7", 60))));
        Assert.Equal((SfuErrorKind.State, "invalid_url"), (error.Kind, error.Code));
        Assert.Equal(0, h.Sfu.ConnCount);
        Assert.Equal(ConnectionState.Disconnected, h.Room.State);
    }

    [Fact]
    public async Task Reconnect_replays_local_state()
    {
        await using var h = Harness.Create();
        h.Sfu.Members = new List<object> { FakeSfu.Member(2) };
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Peer("9002", 2) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        await h.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        await h.Room.LocalParticipant.SetCameraEnabledAsync(true);
        await h.Room.LocalParticipant.SetMetadataAsync("Me again;");
        await h.Room.SetVideoReceivingAsync(new VideoReceiving(true, false));
        var participant = h.Room.GetParticipant(2);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        var c = h.Sfu.Current;
        await h.Wait("camera and visibility replayed", () => c.Messages("camera").Count == 1 && c.Messages("visibility").Count == 1);
        var mute = c.Messages("mute")[0];
        Assert.Equal((false, 1L), (mute["is_mute"]!.GetValue<bool>(), mute["mute_revision"]!.GetValue<long>()));
        Assert.Equal("Me again;", c.Messages("peer_update")[0]["metadata"]!.GetValue<string>());
        Assert.Equal(101u, h.Room.LocalParticipant.PeerId);
        Assert.Same(participant, h.Room.GetParticipant(2));
        await h.Wait("audio subscribed again", () => h.Count("sub:3:Microphone:2") == 2);
        Assert.Equal(1, h.Count("unsub:3:Microphone:2"));
        Assert.True(h.Has("reconnecting"));
    }

    [Fact]
    public async Task Reconnects_when_ice_fails()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.Peer.Fail();
        await h.WaitEvent("reconnected");
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Reconnects_when_ice_stays_disconnected()
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.Peer.Disconnect();
        await Task.Delay(1000);
        Assert.False(h.Has("reconnecting"), "reconnected before five seconds");
        await h.WaitEvent("reconnected", 10);
    }

    [Fact]
    public async Task Reconnects_when_signaling_goes_silent()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { InboundIdle = TimeSpan.FromMilliseconds(400) } });
        await h.Connect();
        await h.WaitEvent("reconnected");
    }

    [Fact]
    public async Task Gives_up_when_the_server_stays_unreachable()
    {
        await using var h = Harness.Create(o => o with { Reconnect = o.Reconnect with { MaxAttempts = 3 } });
        await h.Connect();
        h.Sfu.Refuse = true;
        h.Sfu.Current.DropNow();
        await h.WaitEvent("disconnected:ReconnectExhausted");
        Assert.Equal(ConnectionState.Disconnected, h.Room.State);
    }

    [Theory]
    [InlineData(4006, DisconnectReason.Kicked)]
    [InlineData(4011, DisconnectReason.AloneTimeout)]
    [InlineData(4012, DisconnectReason.DuplicateSession)]
    [InlineData(1000, DisconnectReason.ServerClosed)]
    [InlineData(4003, DisconnectReason.ServerMisconfigured)]
    public async Task Terminal_close_codes_do_not_reconnect(int code, DisconnectReason reason)
    {
        await using var h = Harness.Create();
        await h.Connect();
        h.Sfu.Current.Close(code);
        await h.WaitEvent($"disconnected:{reason}");
        await Task.Delay(100);
        Assert.Equal(1, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task A_late_close_of_a_superseded_session_is_ignored()
    {
        await using var h = Harness.Create();
        await h.Connect();
        var old = h.Sfu.Current;
        h.Sfu.Peer.Fail();
        await h.WaitEvent("reconnected");
        old.Close(4006);
        await Task.Delay(100);
        Assert.Equal(ConnectionState.Connected, h.Room.State);
        Assert.False(h.Has("disconnected:Kicked"));
    }
}
