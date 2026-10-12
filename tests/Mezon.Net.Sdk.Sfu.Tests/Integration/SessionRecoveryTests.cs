using System.Diagnostics;
using System.Text.Json.Nodes;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

public class SessionRecoveryTests
{
    [Fact]
    public async Task A_peer_connection_closed_by_the_stack_reconnects()
    {
        await using var h = Harness.Create();
        await h.Connect();
        await h.WaitEvent("state:Connected");
        h.Sfu.Peer.Config.OnState(PeerState.Closed);
        await h.WaitEvent("reconnecting");
        await h.Wait("a second connection", () => h.Sfu.ConnCount == 2);
    }

    [Fact]
    public async Task A_fatal_answer_error_ends_the_session_at_once()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Admission = TimeSpan.FromSeconds(10) } });
        h.Sfu.NeverConnect = true;
        h.Sfu.On("join", (_, _) =>
        {
            if (h.Sfu.ConnCount > 1)
            {
                h.Sfu.NeverConnect = false;
            }

            return false;
        });
        h.Sfu.On("answer", (c, _) =>
        {
            if (h.Sfu.ConnCount > 1)
            {
                return false;
            }

            c.Send(new { type = "error", message = "duplicate_ice_ufrag" });
            return true;
        });
        var watch = Stopwatch.StartNew();
        await h.Connect();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"the join waited {watch.Elapsed.TotalSeconds:F1}s for the admission timeout");
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Dispose_during_the_connect_backoff_ends_connect_promptly()
    {
        var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(30) } });
        h.Sfu.Refuse = true;
        var connect = h.Connect();
        await Task.Delay(500);
        await h.Room.DisposeAsync();
        var finished = await Task.WhenAny(connect, Task.Delay(3000)) == connect;
        await Assert.ThrowsAnyAsync<Exception>(() => connect);
        await h.DisposeAsync();
        Assert.True(finished, "ConnectAsync kept sleeping through its backoff after the room was disposed");
    }

    [Fact]
    public async Task A_fixed_token_rejected_during_a_reconnect_ends_with_auth_failed()
    {
        await using var h = Harness.Create(o => o with { Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromSeconds(1) } });
        await h.Room.ConnectAsync(h.Sfu.Url, TokenProvider.Fixed(FakeSfu.Token("7", 3600)));
        await h.WaitEvent("state:Connected");
        var watch = Stopwatch.StartNew();
        h.Sfu.Current.Close(4005);
        await h.WaitEvent("disconnected:AuthFailed", 15);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"ended after {watch.Elapsed.TotalSeconds:F1}s");
        Assert.Equal(1, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task An_earlier_session_does_not_reset_the_reconnect_budget()
    {
        await using var h = Harness.Create(o => o with
        {
            StableAfter = TimeSpan.FromSeconds(1),
            Reconnect = new ReconnectOptions { MaxAttempts = 1, Delay = _ => TimeSpan.FromMilliseconds(50) },
        });
        await h.Connect();
        await h.WaitEvent("state:Connected");
        await Task.Delay(700);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        await Task.Delay(500);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("disconnected:ReconnectExhausted");
        Assert.Equal(1, h.Count("reconnected"));
    }

    [Fact]
    public async Task A_cancelled_token_sends_nothing()
    {
        await using var h = Harness.Create();
        await h.Connect();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Room.SendMessageAsync("hi", cancelled.Token));
        await Task.Delay(200);
        Assert.Empty(h.Sfu.Current.Messages("send_message"));
    }

    private static JsonObject InSlot(uint peer, uint slot, long generation) => FakeSfu.MemberWith(peer, m =>
    {
        m["slot"] = slot;
        m["assignment_generation"] = generation;
        m["mid_audio"] = Sdp.SlotFirstMid(slot);
        m["mid_video"] = Sdp.SlotFirstMid(slot) + 1;
        m["mid_screen"] = Sdp.SlotFirstMid(slot) + 2;
    });

    [Fact]
    public async Task Fixed_slot_holders_are_subscribed_again_after_a_reconnect()
    {
        await using var h = Harness.Create();
        h.Sfu.Slots = new List<FixtureSlot> { FixtureSlot.Fixed(0), FixtureSlot.Fixed(1) };
        h.Sfu.Members = new List<object> { InSlot(2, 0, 3), InSlot(3, 1, 5) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        await h.WaitEvent("sub:6:Microphone:3");

        h.Sfu.Members = new List<object> { InSlot(2, 0, 1), FakeSfu.Member(4) };
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        await h.Wait("the slot holder subscribed again", () => h.Count("sub:3:Microphone:2") == 2);
        await h.WaitEvent("left:3");
        h.Sfu.Peer.Deliver(3, 7);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal((ushort)7, (await h.Room.GetParticipant(2)!.AudioTrack!.ReadRtpAsync(timeout.Token))!.SequenceNumber);

        h.Sfu.Current.Send(new
        {
            type = "slot_assigned",
            slot = 1,
            generation = 2,
            peer_id = 4,
            user_id = "9004",
            mid_audio = 6,
            mid_video = 7,
            mid_screen = 8,
            audio_active = true,
            video_active = false,
            screen_active = false,
        });
        await h.WaitEvent("sub:6:Microphone:4");
    }

    private static void ErrorOnJoin(FakeSfu sfu, string code, Func<int, bool> when)
    {
        var joins = 0;
        sfu.On("join", (c, _) =>
        {
            if (!when(Interlocked.Increment(ref joins)))
            {
                return false;
            }

            c.Send(new { type = "error", message = code });
            return true;
        });
    }

    [Fact]
    public async Task Invalid_room_ends_the_first_connect_at_once()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Join = TimeSpan.FromSeconds(10) } });
        ErrorOnJoin(h.Sfu, "invalid_room", _ => true);
        var watch = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        Assert.Equal((SfuErrorKind.Signaling, "invalid_room"), (error.Kind, error.Code));
        await h.WaitEvent("disconnected:JoinFailed");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"ended after {watch.Elapsed.TotalSeconds:F1}s");
        Assert.Equal(1, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Invalid_room_during_a_reconnect_ends_the_room_at_once()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Join = TimeSpan.FromSeconds(10) } });
        ErrorOnJoin(h.Sfu, "invalid_room", n => n > 1);
        await h.Connect();
        h.Sfu.Current.DropNow();
        await h.WaitEvent("disconnected:JoinFailed");
        await Task.Delay(100);
        Assert.Equal(2, h.Sfu.ConnCount);
    }

    [Fact]
    public async Task Room_creation_failed_is_retried_at_once()
    {
        await using var h = Harness.Create(o => o with { Timeouts = o.Timeouts with { Join = TimeSpan.FromSeconds(10) } });
        ErrorOnJoin(h.Sfu, "room_creation_failed", n => n is 1 or 3);
        var watch = Stopwatch.StartNew();
        await h.Connect();
        Assert.Equal(2, h.Sfu.ConnCount);
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        Assert.Equal(4, h.Sfu.ConnCount);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed.TotalSeconds:F1}s");
    }

    [Fact]
    public async Task Push_to_talk_released_before_admission_leaves_the_server_muted()
    {
        await using var h = Harness.Create(o => o with { Role = Role.Audience });
        h.Sfu.On("answer", (c, _) =>
        {
            c.Answered++;
            return true;
        });
        await h.Room.LocalParticipant.SetPushToTalkAsync(true);
        var connect = h.Connect();
        await h.Wait("the initial mute", () => h.Sfu.ConnCount == 1 && h.Sfu.Current.Messages("mute").Count == 1);
        var c = h.Sfu.Current;
        Assert.False(c.Messages("mute")[0]["is_mute"]!.GetValue<bool>());
        await h.Room.LocalParticipant.SetPushToTalkAsync(false);
        await h.Wait("the first answer", () => c.Answered == 1);
        h.Sfu.Snapshot(c);
        await connect;
        await h.Wait("a mute matching push-to-talk off", () => c.Messages("mute").Last()["is_mute"]!.GetValue<bool>());
        Assert.False(h.Room.LocalParticipant.Microphone.IsEnabled);
    }
}
