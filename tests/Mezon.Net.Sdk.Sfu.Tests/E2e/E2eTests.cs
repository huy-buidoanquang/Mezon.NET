using static Mezon.Net.Sdk.Sfu.Tests.E2e.E2e;

namespace Mezon.Net.Sdk.Sfu.Tests.E2e;

/// <summary>
/// Real-SFU tests against the stacks started by <c>dotnet run --project tools/SfuE2e -- up</c>; each skips when its
/// stack is not running.
/// </summary>
[Collection("e2e")]
public class E2eTests
{
    private const byte Loud = 20;
    private const byte Silent = 127;

    private static Stack Require(string mode) => Stack.Get(mode) ?? throw new InvalidOperationException($"the {mode} stack is not running");

    private static DownlinkMode ModeOf(string mode) => mode switch
    {
        "fixed2" => DownlinkMode.Fixed,
        "decoupled" => DownlinkMode.Decoupled,
        _ => DownlinkMode.Dynamic,
    };

    [E2eFact("dynamic")]
    public Task Join_and_hear_dynamic() => JoinAndHear("dynamic");

    [E2eFact("fixed2")]
    public Task Join_and_hear_fixed2() => JoinAndHear("fixed2");

    [E2eFact("decoupled")]
    public Task Join_and_hear_decoupled() => JoinAndHear("decoupled");

    private static async Task JoinAndHear(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice Nguyễn 😀");
        await alice.Publish((byte)'A', () => 30);
        await using var bob = await Join(stack, roomId, "Bob");
        Assert.Equal(ConnectionState.Connected, alice.Room.State);
        Assert.Equal(bob.UserId, bob.Room.LocalParticipant.UserId);
        await WaitFor(5, "Bob sees Alice", () => bob.Room.GetParticipantsByUserId(alice.UserId).Count == 1);
        var seen = bob.Room.GetParticipantsByUserId(alice.UserId)[0];
        Assert.Equal("Alice Nguyễn 😀", seen.Info.DisplayName);
        await WaitFor(10, "Bob receives Alice's frames", () => bob.Frames(alice.UserId, (byte)'A') > 50);
        Assert.Equal(ModeOf(mode), bob.Room.DownlinkMode);
        await WaitFor(5, "Alice is an active speaker", () => bob.Room.ActiveSpeakers.Any(s => s.UserId == alice.UserId));
        Assert.False(seen.IsMuted);
        Assert.Equal("audio/opus", seen.AudioTrack?.MimeType);
    }

    [E2eFact("dynamic")]
    public Task Mute_unmute_dynamic() => MuteUnmute("dynamic");

    [E2eFact("fixed2")]
    public Task Mute_unmute_fixed2() => MuteUnmute("fixed2");

    [E2eFact("decoupled")]
    public Task Mute_unmute_decoupled() => MuteUnmute("decoupled");

    private static async Task MuteUnmute(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await alice.Publish((byte)'A', () => 30);
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(10, "Bob receives Alice", () => bob.Frames(alice.UserId, (byte)'A') > 20);
        await alice.Room.LocalParticipant.SetMicrophoneEnabledAsync(false);
        await WaitFor(5, "Bob sees Alice muted", () => bob.Room.GetParticipantsByUserId(alice.UserId).FirstOrDefault()?.IsMuted == true);
        await Task.Delay(500);
        var before = bob.Frames(alice.UserId, (byte)'A');
        await Task.Delay(1500);
        Assert.True(bob.Frames(alice.UserId, (byte)'A') - before <= 5, $"{mode}: frames kept flowing while muted");
        await alice.Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        await WaitFor(5, "Bob sees Alice unmuted", () => bob.Room.GetParticipantsByUserId(alice.UserId).FirstOrDefault()?.IsMuted == false);
        var resumed = bob.Frames(alice.UserId, (byte)'A');
        await WaitFor(8, "audio resumes", () => bob.Frames(alice.UserId, (byte)'A') > resumed + 30);
    }

    [E2eFact("dynamic")]
    public Task Room_messages_dynamic() => RoomMessages("dynamic");

    [E2eFact("fixed2")]
    public Task Room_messages_fixed2() => RoomMessages("fixed2");

    [E2eFact("decoupled")]
    public Task Room_messages_decoupled() => RoomMessages("decoupled");

    private static async Task RoomMessages(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(5, "Bob sees Alice", () => bob.Room.RemoteParticipants.Count == 1);
        const string text = "xin chào \"Bob\" 👋 <b>&</b>\nline 2 \\ /";
        await alice.Room.SendMessageAsync(text);
        await WaitFor(5, "the message", () => bob.Messages.Count == 1);
        Assert.Equal($"{alice.UserId}: {text}", bob.Messages[0]);
    }

    [E2eFact("dynamic")]
    public Task Participant_leaves_dynamic() => ParticipantLeaves("dynamic");

    [E2eFact("fixed2")]
    public Task Participant_leaves_fixed2() => ParticipantLeaves("fixed2");

    [E2eFact("decoupled")]
    public Task Participant_leaves_decoupled() => ParticipantLeaves("decoupled");

    private static async Task ParticipantLeaves(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await alice.Publish((byte)'A', () => 30);
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(10, "Bob receives Alice", () => bob.Frames(alice.UserId, (byte)'A') > 10);
        alice.StopFrames();
        await alice.Room.DisconnectAsync();
        await WaitFor(5, "the participant-disconnected event", () => bob.Has($"left:{alice.UserId}"));
        Assert.True(bob.Has($"unsubscribed:Microphone:{alice.UserId}"), $"{mode}: audio not unsubscribed");
        Assert.Empty(bob.Room.RemoteParticipants);
    }

    [E2eFact("dynamic")]
    public Task Duplicate_session_dynamic() => DuplicateSession("dynamic");

    [E2eFact("fixed2")]
    public Task Duplicate_session_fixed2() => DuplicateSession("fixed2");

    [E2eFact("decoupled")]
    public Task Duplicate_session_decoupled() => DuplicateSession("decoupled");

    private static async Task DuplicateSession(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        var userId = Peer.RandomId();
        await using var first = await Join(stack, roomId, "First", userId: userId);
        await using var second = await Join(stack, roomId, "Second", userId: userId);
        await WaitFor(5, "the first session is disconnected", () => first.Has("disconnected:DuplicateSession"));
        Assert.Equal(ConnectionState.Disconnected, first.Room.State);
        Assert.Equal(ConnectionState.Connected, second.Room.State);
    }

    private static byte[] Vp8Frame(bool keyframe, int index)
    {
        byte[] frame;
        if (keyframe)
        {
            frame = new byte[1200];
            new byte[] { 0x10, 0x02, 0x00, 0x9d, 0x01, 0x2a, 0x80, 0x02, 0x68, 0x01 }.CopyTo(frame, 0);
        }
        else
        {
            frame = new byte[400];
            new byte[] { 0x11, 0x00, 0x00 }.CopyTo(frame, 0);
        }

        frame[20] = (byte)index;
        return frame;
    }

    /// <summary>Writes VP8 frames at 30 fps, a keyframe every second, until <paramref name="stop"/>.</summary>
    private static Task WriteCamera(Peer peer, CancellationToken stop) => Task.Run(async () =>
    {
        var camera = peer.Room.LocalParticipant.Camera;
        for (var i = 0; !stop.IsCancellationRequested; i++)
        {
            camera.WriteVp8(Vp8Frame(i % 30 == 0, i), TimeSpan.FromMilliseconds(33));
            try
            {
                await Task.Delay(33, stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    });

    [E2eFact("dynamic")]
    public async Task Camera_video_is_forwarded()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(5, "Bob sees Alice", () => bob.Room.GetParticipantsByUserId(alice.UserId).Count == 1);
        using var stop = new CancellationTokenSource();
        var writer = WriteCamera(alice, stop.Token);
        await alice.Room.LocalParticipant.SetCameraEnabledAsync(true);
        RemoteTrack? track = null;
        await WaitFor(10, "Bob subscribes Alice's camera", () => (track = bob.Room.GetParticipantsByUserId(alice.UserId).FirstOrDefault()?.CameraTrack) is not null);
        await WaitFor(10, "camera packets", () => track!.Stats.PacketsReceived > 30);
        Assert.Equal("video/vp8", track!.MimeType?.ToLowerInvariant());
        var publisher = bob.Room.GetParticipantsByUserId(alice.UserId)[0];
        await bob.Room.RequestKeyframeAsync(publisher, TrackSource.Camera);
        await alice.Room.LocalParticipant.SetCameraEnabledAsync(false);
        await WaitFor(5, "camera unsubscribed", () => bob.Has($"unsubscribed:Camera:{alice.UserId}"));
        Assert.True(track.IsEnded);
        stop.Cancel();
        await writer;
    }

    [E2eFact("decoupled")]
    public async Task Decoupled_camera_slots_forward_video_and_report_capacity()
    {
        var stack = Require("decoupled");
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await using var bob = await Join(stack, roomId, "Bob");
        await using var carol = await Join(stack, roomId, "Carol");
        await WaitFor(5, "Bob sees Alice and Carol", () => bob.Room.RemoteParticipants.Count == 2);

        // mezon-sfu 19f885a refuses to stop the camera of a publisher that holds no other slot (camera_update_failed).
        await alice.Publish((byte)'A', () => 30);
        await WaitFor(10, "Bob receives Alice", () => bob.Frames(alice.UserId, (byte)'A') > 10);
        using var stop = new CancellationTokenSource();
        var writers = new[] { WriteCamera(alice, stop.Token), WriteCamera(carol, stop.Token) };
        await alice.Room.LocalParticipant.SetCameraEnabledAsync(true);
        RemoteTrack? track = null;
        await WaitFor(10, "Bob subscribes Alice's camera", () => (track = bob.Room.GetParticipantsByUserId(alice.UserId).FirstOrDefault()?.CameraTrack) is not null);
        await WaitFor(10, "camera packets", () => track!.Stats.PacketsReceived > 30);

        // decoupled.ini.template has one camera slot, held by Alice.
        var full = await Assert.ThrowsAsync<SfuException>(() => carol.Room.LocalParticipant.SetCameraEnabledAsync(true));
        Assert.Equal((SfuErrorKind.Signaling, "camera_capacity_full", true), (full.Kind, full.Code, full.Retryable));
        Assert.Equal(ConnectionState.Connected, carol.Room.State);

        await alice.Room.LocalParticipant.SetCameraEnabledAsync(false);
        await WaitFor(5, "Alice's camera unsubscribed", () => bob.Has($"unsubscribed:Camera:{alice.UserId}"));
        Assert.True(track!.IsEnded);
        await carol.Room.LocalParticipant.SetCameraEnabledAsync(true);
        await WaitFor(10, "Bob receives Carol's camera", () => bob.Room.GetParticipantsByUserId(carol.UserId).FirstOrDefault()?.CameraTrack is { Stats.PacketsReceived: > 30 });
        stop.Cancel();
        await Task.WhenAll(writers);
    }

    [E2eFact("dynamic")]
    public Task Audience_push_to_talk() => AudiencePushToTalk("dynamic");

    [E2eFact("decoupled")]
    public Task Audience_push_to_talk_decoupled() => AudiencePushToTalk("decoupled");

    private static async Task AudiencePushToTalk(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        await using var speaker = await Join(stack, roomId, "Speaker");
        await using var listener = await Join(stack, roomId, "Listener", Options(Role.Audience));
        await using var other = await Join(stack, roomId, "Other", Options(Role.Audience));
        listener.StartFrames((byte)'L', () => 30);
        await WaitFor(5, "the speaker sees the audience member", () => speaker.Room.GetParticipantsByUserId(listener.UserId).Count == 1);
        await Task.Delay(1500);
        Assert.Equal(0, speaker.Frames(listener.UserId, (byte)'L'));
        await Assert.ThrowsAsync<SfuException>(() => listener.Room.LocalParticipant.SetCameraEnabledAsync(true));
        await Assert.ThrowsAsync<SfuException>(() => speaker.Room.LocalParticipant.SetPushToTalkAsync(true));
        await listener.Room.LocalParticipant.SetPushToTalkAsync(true);
        await WaitFor(10, "the speaker hears push-to-talk", () => speaker.Frames(listener.UserId, (byte)'L') > 20);
        await WaitFor(10, "the other audience member hears push-to-talk (mezon-sfu#482)", () => other.Frames(listener.UserId, (byte)'L') > 20);
        await listener.Room.LocalParticipant.SetPushToTalkAsync(false);
        await Task.Delay(500);
        var before = speaker.Frames(listener.UserId, (byte)'L');
        await Task.Delay(1500);
        Assert.True(speaker.Frames(listener.UserId, (byte)'L') - before <= 5, "still heard after releasing push-to-talk");
    }

    [E2eFact("fixed2")]
    public Task Fixed_slots_follow_voice_activity() => SlotsFollowVoiceActivity("fixed2");

    [E2eFact("decoupled")]
    public Task Decoupled_audio_slots_follow_voice_activity() => SlotsFollowVoiceActivity("decoupled");

    /// <summary>Both stacks have two audio slots for three publishers.</summary>
    private static async Task SlotsFollowVoiceActivity(string mode)
    {
        var stack = Require(mode);
        var roomId = Peer.RandomId();
        var levels = new[] { Loud, Loud, Silent };
        var peers = new List<Peer>();
        try
        {
            foreach (var (name, marker, index) in new[] { ("A", (byte)'A', 0), ("B", (byte)'B', 1), ("C", (byte)'C', 2) })
            {
                var peer = await Join(stack, roomId, name);
                await peer.Publish(marker, () => Volatile.Read(ref levels[index]));
                peers.Add(peer);
            }

            await using var d = await Join(stack, roomId, "D");
            var (a, b, c) = (peers[0].UserId, peers[1].UserId, peers[2].UserId);
            await WaitFor(10, "D receives A and B", () => d.Frames(a, (byte)'A') > 20 && d.Frames(b, (byte)'B') > 20);
            Assert.Equal(0, d.Frames(c, (byte)'C'));
            Volatile.Write(ref levels[0], Silent);
            Volatile.Write(ref levels[2], Loud);
            await WaitFor(10, "D receives C once A falls silent", () => d.Frames(c, (byte)'C') > 20);
            await WaitFor(5, "C is an active speaker", () => d.Room.ActiveSpeakers.Any(s => s.UserId == c));
            Assert.True(d.Room.ActiveSpeakers.Count <= 2);
        }
        finally
        {
            foreach (var p in peers)
            {
                await p.DisposeAsync();
            }
        }
    }

    [E2eFact("dynamic")]
    public async Task Metadata_and_visibility()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(5, "Bob sees Alice", () => bob.Room.GetParticipantsByUserId(alice.UserId).Count == 1);
        await alice.Room.LocalParticipant.SetMetadataAsync("Alice Renamed;https://example.com/a.png");
        await WaitFor(5, "Bob sees the new name", () => bob.Room.GetParticipantsByUserId(alice.UserId)[0].Info.DisplayName == "Alice Renamed");
        Assert.Equal(new VideoReceiving(false, true), await bob.Room.SetVideoReceivingAsync(new VideoReceiving(false, true)));
    }

    [E2eFact("dynamic")]
    public async Task Reconnect_after_a_socket_cut()
    {
        var stack = Require("dynamic");
        using var proxy = Proxy.Start(stack);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack.Via(proxy), roomId, "Alice");
        await alice.Publish((byte)'A', () => 30);
        await using var bob = await Join(stack, roomId, "Bob");
        await WaitFor(10, "Bob receives Alice", () => bob.Frames(alice.UserId, (byte)'A') > 10);
        var before = alice.Room.LocalParticipant.PeerId;
        proxy.Cut();
        await WaitFor(15, "Alice reconnected", () => alice.Has("reconnected"));
        Assert.True(alice.Has("reconnecting"));
        Assert.Equal(ConnectionState.Connected, alice.Room.State);
        var after = alice.Room.LocalParticipant.PeerId;
        Assert.True(after != before && after != 0, $"peer id {after} after reconnect (was {before})");
        Assert.True(alice.Room.LocalParticipant.IsMicrophoneEnabled);
        var resumed = bob.Frames(alice.UserId, (byte)'A');
        await WaitFor(15, "Bob receives the new session of Alice", () =>
        {
            var sessions = bob.Room.GetParticipantsByUserId(alice.UserId);
            return sessions.Count == 1 && !sessions[0].IsMuted && bob.Frames(alice.UserId, (byte)'A') > resumed + 20;
        });
    }

    [E2eFact("fixed2")]
    public Task Reconnect_in_a_fixed_slot_room_hears_the_existing_publisher() => ReconnectHearsTheExistingPublisher("fixed2");

    [E2eFact("decoupled")]
    public Task Reconnect_in_a_decoupled_room_hears_the_existing_publisher() => ReconnectHearsTheExistingPublisher("decoupled");

    private static async Task ReconnectHearsTheExistingPublisher(string mode)
    {
        var stack = Require(mode);
        using var proxy = Proxy.Start(stack);
        var roomId = Peer.RandomId();
        await using var alice = await Join(stack, roomId, "Alice");
        await alice.Publish((byte)'A', () => 30);
        await using var bob = await Join(stack.Via(proxy), roomId, "Bob");
        await WaitFor(10, "Bob receives Alice", () => bob.Frames(alice.UserId, (byte)'A') > 20);
        proxy.Cut();
        await WaitFor(15, "Bob reconnected", () => bob.Has("reconnected"));
        var resumed = bob.Frames(alice.UserId, (byte)'A');
        await WaitFor(10, "Bob hears Alice again", () => bob.Frames(alice.UserId, (byte)'A') > resumed + 20);
        Assert.Equal(ModeOf(mode), bob.Room.DownlinkMode);
    }

    [E2eFact("dynamic")]
    public async Task Token_refresh_on_reconnect()
    {
        var stack = Require("dynamic");
        using var proxy = Proxy.Start(stack);
        var roomId = Peer.RandomId();
        var userId = Peer.RandomId();
        var reasons = new List<TokenReason>();
        var tokens = TokenProvider.Create((request, _) =>
        {
            TimeSpan ttl;
            lock (reasons)
            {
                reasons.Add(request.Reason);
                ttl = reasons.Count == 1 ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(10);
            }

            return Task.FromResult(stack.Token(userId, roomId, "", ttl));
        });
        await using var room = await Join(stack.Via(proxy), roomId, "Refresher", userId: userId, tokens: tokens);
        proxy.Cut();
        await WaitFor(15, "reconnected", () => room.Has("reconnected"));
        lock (reasons)
        {
            Assert.Equal(new[] { TokenReason.Initial, TokenReason.Expiring }, reasons);
        }
    }

    private static RoomOptions Moderator(Stack stack, string roomId) => Options() with
    {
        ModerationTokens = ModerationTokenProvider.Create((action, userId, _) => Task.FromResult(stack.Token(userId, roomId, action, TimeSpan.FromSeconds(60)))),
    };

    [E2eFact("dynamic")]
    public async Task Kick()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        await using var moderator = await Join(stack, roomId, "Mod", Moderator(stack, roomId));
        await using var target = await Join(stack, roomId, "Target");
        await target.Publish((byte)'T', () => 30);
        await WaitFor(10, "the moderator receives the target", () => moderator.Frames(target.UserId, (byte)'T') > 5);
        Assert.Equal(1, await moderator.Room.KickParticipantAsync(target.UserId));
        await WaitFor(5, "the target is kicked", () => target.Has("disconnected:Kicked"));
        await WaitFor(5, "the target is gone", () => moderator.Room.GetParticipantsByUserId(target.UserId).Count == 0);
    }

    [E2eFact("dynamic")]
    public async Task Moderator_mute()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        await using var moderator = await Join(stack, roomId, "Mod", Moderator(stack, roomId));
        await using var target = await Join(stack, roomId, "Target");
        await target.Publish((byte)'T', () => 30);
        await WaitFor(5, "the target is unmuted", () => moderator.Room.GetParticipantsByUserId(target.UserId).FirstOrDefault()?.IsMuted == false);
        Assert.Equal(1, await moderator.Room.MuteParticipantAsync(target.UserId));
        await WaitFor(5, "a forced mute on the target", () => target.Has("forced-mute"));
        Assert.False(target.Room.LocalParticipant.IsMicrophoneEnabled);
        await WaitFor(5, "the moderator sees the target muted", () => moderator.Room.GetParticipantsByUserId(target.UserId).FirstOrDefault()?.IsMuted == true);
    }

    [E2eFact("dynamic")]
    public async Task Leave_right_after_join_is_announced()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        await using var stayer = await Join(stack, roomId, "Stayer");
        await using var leaver = await Join(stack, roomId, "Leaver");
        await WaitFor(5, "the stayer sees the leaver", () => stayer.Room.GetParticipantsByUserId(leaver.UserId).Count == 1);
        await leaver.Room.DisconnectAsync();
        await WaitFor(8, "the leaver is gone", () => stayer.Room.GetParticipantsByUserId(leaver.UserId).Count == 0);
    }

    [E2eFact("dynamic")]
    public async Task Moderation_tokens_cannot_join()
    {
        var stack = Require("dynamic");
        var roomId = Peer.RandomId();
        var victimId = Peer.RandomId();
        await using var victim = await Join(stack, roomId, "Victim", userId: victimId);
        var kick = stack.Token(victimId, roomId, "kick", TimeSpan.FromSeconds(60));
        var options = Options() with { Reconnect = new ReconnectOptions { Enabled = false } };
        var (impostor, error) = await TryJoin(stack, roomId, "Impostor", options, victimId, TokenProvider.Fixed(kick));
        await using (impostor)
        {
            Assert.NotNull(error);
            await Task.Delay(500);
            Assert.False(victim.Has("disconnected:DuplicateSession"), "the victim was evicted");
        }
    }

    [E2eFact("alone")]
    public async Task Alone_timeout()
    {
        var stack = Require("alone");
        using var proxy = Proxy.Start(stack);
        await using var alone = await Join(stack.Via(proxy), Peer.RandomId(), "Alone");
        await WaitFor(20, "the alone timeout", () => alone.Has("disconnected:AloneTimeout"));
        await Task.Delay(1000);
        Assert.Equal(1, proxy.Count);
    }
}
