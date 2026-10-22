using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

/// <summary>Decoupled media pools (mezon-sfu <c>media_pool_mode = decoupled</c>) against the scripted server.</summary>
public class DecoupledTests
{
    // 2 audio, 1 camera and 1 screen slots: audio mids 3 and 4, camera mid 5, screen mid 6.
    private static Harness Create()
    {
        var h = Harness.Create();
        h.Sfu.Pool = new MediaPoolLayout(2, 1, 1);
        return h;
    }

    private static object Assigned(string kind, uint slot, long generation, uint mid, uint peer, bool active) => new
    {
        type = "slot_assigned",
        kind,
        slot,
        generation,
        mid,
        peer_id = peer,
        user_id = (9000 + peer).ToString(),
        active,
    };

    private static object Released(string kind, uint slot, long generation) => new { type = "slot_released", kind, slot, generation };

    private static uint[] Speakers(Harness h) => h.Room.ActiveSpeakers.Select(p => p.PeerId).ToArray();

    [Fact]
    public async Task Joins_with_the_media_pool_version_and_reports_the_mode()
    {
        await using var h = Create();
        await h.Connect();
        await h.WaitEvent("mode:Decoupled");
        Assert.Equal(DownlinkMode.Decoupled, h.Room.DownlinkMode);
        Assert.Equal(1, h.Sfu.Current.Messages("join")[0]["media_pool_version"]!.GetValue<int>());
    }

    [Fact]
    public async Task Tracks_follow_the_slot_events_of_each_pool()
    {
        await using var h = Create();
        h.Sfu.Members = new List<object> { FakeSfu.PoolMember(2), FakeSfu.PoolMember(3) };
        await h.Connect();
        await h.WaitEvent("mode:Decoupled");
        var c = h.Sfu.Current;
        c.Send(Assigned("audio", 1, 1, 4, 2, true));
        await h.WaitEvent("sub:4:Microphone:2");
        Assert.Equal(new uint[] { 2 }, Speakers(h));
        c.Send(Assigned("audio", 1, 1, 4, 2, false));
        await h.WaitEvent("muted:4");
        Assert.Empty(Speakers(h));
        c.Send(Assigned("audio", 1, 1, 4, 2, true));
        await h.WaitEvent("unmuted:4");

        // Camera and screen slots are assigned on request; their tracks follow camera_active and screen_active.
        c.Send(Assigned("camera", 0, 1, 5, 2, true));
        c.Send(Assigned("screen", 0, 1, 6, 3, true));
        var camera = FakeSfu.PoolMember(2);
        camera["camera_requested"] = true;
        camera["camera_active"] = true;
        c.Send(new { type = "peer_updated", peer = camera });
        await h.WaitEvent("sub:5:Camera:2");
        Assert.False(h.Has("sub:6:Screen:3"));
        var screen = FakeSfu.PoolMember(3);
        screen["screen_requested"] = true;
        screen["screen_active"] = true;
        c.Send(new { type = "peer_updated", peer = screen });
        await h.WaitEvent("sub:6:Screen:3");

        // A newer generation moves a slot; late generations, other pools' mids and unknown kinds are ignored.
        c.Send(Assigned("audio", 1, 2, 4, 3, true));
        await h.WaitEvent("sub:4:Microphone:3");
        Assert.True(h.Has("unsub:4:Microphone:2"));
        c.Send(Assigned("audio", 1, 1, 4, 2, true));
        c.Send(Released("audio", 1, 1));
        c.Send(Assigned("audio", 0, 1, 5, 2, true));
        c.Send(Assigned("data", 0, 1, 3, 2, true));
        c.Send(Released("camera", 1, 9));
        await Task.Delay(50);
        Assert.Equal(4u, h.Room.GetParticipant(3)!.AudioTrack!.Mid);
        Assert.Null(h.Room.GetParticipant(2)!.AudioTrack);
        Assert.NotNull(h.Room.GetParticipant(2)!.CameraTrack);
        Assert.Equal(new uint[] { 3 }, Speakers(h));

        c.Send(Released("audio", 1, 2));
        await h.WaitEvent("unsub:4:Microphone:3");
        Assert.Empty(Speakers(h));
        c.Send(Released("camera", 0, 1));
        await h.WaitEvent("unsub:5:Camera:2");
    }

    [Fact]
    public async Task Membership_seeds_the_pools_and_departures_free_them()
    {
        await using var h = Create();
        var holder = FakeSfu.PoolMember(2, audio: FakeSfu.PoolSlot(0, 3, 3), camera: FakeSfu.PoolSlot(0, 1, 5));
        holder["camera_requested"] = true;
        holder["camera_active"] = true;
        h.Sfu.Members = new List<object> { holder, FakeSfu.PoolMember(3) };
        await h.Connect();
        await h.WaitEvent("sub:5:Camera:2");
        Assert.True(h.Has("sub:3:Microphone:2"));
        Assert.Equal(new uint[] { 2 }, Speakers(h));
        var c = h.Sfu.Current;
        c.Send(Assigned("audio", 1, 5, 4, 3, true));
        await h.WaitEvent("sub:4:Microphone:3");

        // peer_joined seeds the newcomer's slots, but never over a generation this client already knows.
        var newcomer = FakeSfu.PoolMember(4, audio: FakeSfu.PoolSlot(1, 4, 4), screen: FakeSfu.PoolSlot(0, 2, 6));
        newcomer["screen_requested"] = true;
        newcomer["screen_active"] = true;
        c.Send(new { type = "peer_joined", room_revision = 2, participant_count = 4, peer = newcomer });
        await h.WaitEvent("sub:6:Screen:4");
        Assert.Equal(4u, h.Room.GetParticipant(3)!.AudioTrack?.Mid);
        Assert.Null(h.Room.GetParticipant(4)!.AudioTrack);

        // peer_left frees every slot of the peer and retires the generations it lists.
        var slots = new { audio = FakeSfu.PoolSlot(0, 3, 3), camera = FakeSfu.PoolSlot(0, 1, 5), screen = (object?)null };
        c.Send(new { type = "peer_left", room_revision = 3, participant_count = 3, ufrag = "u2", user_id = "9002", peer_id = 2, media_slots = slots });
        await h.WaitEvent("left:2");
        Assert.True(h.Has("unsub:3:Microphone:2") && h.Has("unsub:5:Camera:2"));
        Assert.Equal(new uint[] { 3 }, Speakers(h));
        c.Send(Assigned("audio", 0, 3, 3, 4, true));
        await Task.Delay(50);
        Assert.False(h.Has("sub:3:Microphone:4"));
        c.Send(Assigned("audio", 0, 4, 3, 4, true));
        await h.WaitEvent("sub:3:Microphone:4");
    }

    [Fact]
    public async Task A_reconnect_reseeds_the_pools_from_the_new_snapshot()
    {
        await using var h = Create();
        h.Sfu.Members = new List<object> { FakeSfu.PoolMember(2, audio: FakeSfu.PoolSlot(0, 7, 3)) };
        await h.Connect();
        await h.WaitEvent("sub:3:Microphone:2");
        var participant = h.Room.GetParticipant(2);
        h.Sfu.Members = new List<object> { FakeSfu.PoolMember(2, audio: FakeSfu.PoolSlot(1, 1, 4)) };
        h.Sfu.Current.DropNow();
        await h.WaitEvent("reconnected");
        await h.WaitEvent("sub:4:Microphone:2");
        Assert.True(h.Has("unsub:3:Microphone:2"));
        Assert.Same(participant, h.Room.GetParticipant(2));
        Assert.Equal(new uint[] { 2 }, Speakers(h));
        Assert.Equal(1, h.Sfu.Current.Messages("join")[0]["media_pool_version"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_full_camera_pool_fails_the_request_and_keeps_the_session()
    {
        await using var h = Create();
        await h.Connect();
        h.Sfu.On("camera", (conn, _) =>
        {
            conn.Send(new { type = "error", message = "camera_capacity_full" });
            return true;
        });
        var error = await Assert.ThrowsAsync<SfuException>(() => h.Room.LocalParticipant.SetCameraEnabledAsync(true));
        Assert.Equal((SfuErrorKind.Signaling, "camera_capacity_full", "camera", true), (error.Kind, error.Code, error.Operation, error.Retryable));
        Assert.True(h.Room.LocalParticipant.IsCameraEnabled);
        Assert.Equal(ConnectionState.Connected, h.Room.State);
    }

    [Fact]
    public async Task An_unsupported_media_pool_version_ends_with_protocol_rejected()
    {
        await using var h = Create();
        h.Sfu.On("join", (conn, _) =>
        {
            conn.Send(new { type = "error", message = "unsupported_media_pool_version" });
            conn.Close(1002);
            return true;
        });
        await Assert.ThrowsAsync<SfuException>(() => h.Connect());
        await h.WaitEvent("disconnected:ProtocolRejected");
        Assert.Equal(2, h.Sfu.ConnCount);
    }
}
