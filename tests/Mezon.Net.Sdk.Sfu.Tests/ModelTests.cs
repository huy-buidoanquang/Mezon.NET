using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu.Tests;

public class ModelTests
{
    private static readonly TimeSpan T0 = TimeSpan.FromHours(1);

    private static TimeSpan At(double seconds) => T0 + TimeSpan.FromSeconds(seconds);

    // -------------------------------------------------------------------------------------------
    // SDP

    [Fact]
    public void Parse_sections_and_occupants()
    {
        var offer = OfferFixtures.BuildOffer(new OfferOptions(Slots: new[] { FixtureSlot.Peer("1840651530236071936", 7), FixtureSlot.Empty }));
        var parsed = Sdp.Parse(offer);
        Assert.Equal(("srvufrag", 9), (parsed.IceUfrag, parsed.Sections.Count));
        var s = parsed.Sections[3];
        Assert.Equal(("audio", "3", "sendonly", 7000), (s.Media, s.Mid, s.Direction, s.Port));
        Assert.Equal("OPUS", s.Codecs[111]);
        Assert.Equal(new Occupant.Peer("1840651530236071936", 7), s.Occupant);
        Assert.Equal(new uint[] { 1003 }, s.Ssrcs);
        Assert.Null(parsed.Sections[6].Occupant);
        Assert.Equal("inactive", parsed.Sections[6].Direction);
    }

    [Fact]
    public void Fixtures_use_the_servers_header_extensions()
    {
        var offer = OfferFixtures.BuildOffer(new OfferOptions(Slots: new[] { FixtureSlot.Peer("9", 2), FixtureSlot.Empty, FixtureSlot.Fixed(2) }));
        var ids = Sdp.Parse(offer).Sections.Select(s => string.Join(",", s.Lines.Where(l => l.StartsWith("a=extmap:")).Select(l => l.Substring(9, 1)))).ToArray();
        Assert.Equal(new[] { "6,7,8", "6,7", "6,7", "7", "7,6", "7", "7", "7", "7", "7", "7,6", "7" }, ids);
        Assert.Contains("a=extmap:6 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01", offer);
        Assert.Contains("a=extmap:7 urn:ietf:params:rtp-hdrext:sdes:mid", offer);
        Assert.Contains("a=extmap:8 urn:ietf:params:rtp-hdrext:ssrc-audio-level", offer);
    }

    [Fact]
    public void Msid_forms()
    {
        foreach (var (line, want) in new (string, Occupant?)[]
                 {
                     ("a=msid:slot-3 audio-slot-3", new Occupant.Slot(3)),
                     ("a=msid:u42-p9 video-u42-p9", new Occupant.Peer("42", 9)),
                     ("a=msid:screen-u42-p9 screen-u42-p9", new Occupant.Peer("42", 9)),
                     ("a=msid:u42-p0 audio-u42-p0", null),
                     ("a=msid:stream track", null),
                     ("a=msid:u0042-p9 audio", new Occupant.Peer("42", 9)),
                 })
        {
            var parsed = Sdp.Parse($"v=0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=mid:3\r\n{line}\r\n");
            Assert.Equal(want, parsed.Sections[0].Occupant);
        }
    }

    [Fact]
    public void Remote_mid_layout()
    {
        Assert.Null(Sdp.RemoteMidInfo(2));
        Assert.Equal(new RemoteMid(0, TrackSource.Microphone), Sdp.RemoteMidInfo(3));
        Assert.Equal(new RemoteMid(1, TrackSource.Camera), Sdp.RemoteMidInfo(7));
        Assert.Equal(new RemoteMid(2, TrackSource.Screen), Sdp.RemoteMidInfo(11));
        Assert.Equal(15u, Sdp.SlotFirstMid(4));
    }

    [Fact]
    public void Analyze_offer_detects_the_downlink_mode()
    {
        var none = Sdp.AnalyzeOffer(OfferFixtures.BuildOffer(new OfferOptions()));
        Assert.Equal((DownlinkMode.Unknown, 0u), (none.Mode, none.SlotCount));
        var dynamic = Sdp.AnalyzeOffer(OfferFixtures.BuildOffer(new OfferOptions(Slots: new[] { FixtureSlot.Empty, FixtureSlot.Peer("5", 2) })));
        Assert.Equal((DownlinkMode.Dynamic, 2u, 6), (dynamic.Mode, dynamic.SlotCount, dynamic.Remote.Count));
        var r = dynamic.Remote[3];
        Assert.Equal((6u, 1u, TrackSource.Microphone, "sendonly"), (r.Mid, r.Slot, r.Source, r.Direction));
        var fixedLayout = Sdp.AnalyzeOffer(OfferFixtures.BuildOffer(new OfferOptions(Slots: new[] { FixtureSlot.Fixed(0), FixtureSlot.Fixed(1) })));
        Assert.Equal((DownlinkMode.Fixed, 2u), (fixedLayout.Mode, fixedLayout.SlotCount));
    }

    private static string Answer(params string[] mids)
    {
        var s = "v=0\r\n";
        for (var i = 0; i < mids.Length; i++)
        {
            s += i % 3 == 0 ? "m=audio 9 UDP/TLS/RTP/SAVPF 96\r\n" : "m=video 9 UDP/TLS/RTP/SAVPF 96\r\n";
            if (mids[i].Length > 0)
            {
                s += $"a=mid:{mids[i]}\r\n";
            }
        }

        return s;
    }

    [Fact]
    public void Answer_shape_check_mirrors_the_server()
    {
        Assert.Null(Sdp.CheckAnswerShape(Answer("0", "1", "2", "3", "4", "5"), 1));
        Assert.Contains("expected 6", Sdp.CheckAnswerShape(Answer("0", "1", "2"), 1));
        Assert.NotNull(Sdp.CheckAnswerShape(Answer("0", "2", "1"), 0));
        Assert.NotNull(Sdp.CheckAnswerShape(Answer("0", "1", ""), 0));
        Assert.NotNull(Sdp.CheckAnswerShape(ReplaceFirst(Answer("0", "1", "2"), "m=audio", "m=video"), 0));
    }

    private static string ReplaceFirst(string s, string from, string to)
    {
        var at = s.IndexOf(from, StringComparison.Ordinal);
        return s.Substring(0, at) + to + s.Substring(at + from.Length);
    }

    [Fact]
    public void Answer_directions_follow_the_protocol()
    {
        Assert.Equal("sendonly", Sdp.AnswerDirection(0, "recvonly", camera: false));
        Assert.Equal("sendonly", Sdp.AnswerDirection(1, "recvonly", camera: true));
        Assert.Equal("inactive", Sdp.AnswerDirection(1, "recvonly", camera: false));
        Assert.Equal("inactive", Sdp.AnswerDirection(1, "inactive", camera: true));
        Assert.Equal("inactive", Sdp.AnswerDirection(2, "recvonly", camera: true));
        Assert.Equal("recvonly", Sdp.AnswerDirection(3, "sendonly", camera: true));
        Assert.Equal("inactive", Sdp.AnswerDirection(4, "inactive", camera: true));
    }

    [Fact]
    public void Normalize_answer_rewrites_directions_payloads_bundle_and_missing_sections()
    {
        var offer = OfferFixtures.BuildOffer(new OfferOptions(Slots: new[] { FixtureSlot.Peer("9", 2), FixtureSlot.Empty }));
        // A stack's answer with wrong directions, an extra payload type and a missing section (mid 8).
        var raw = string.Join("\r\n", new[]
        {
            "v=0", "o=- 1 0 IN IP4 127.0.0.1", "s=x", "t=0 0", "a=group:BUNDLE 0 1 2 3 4 5 6 7",
            "m=audio 9 UDP/TLS/RTP/SAVPF 111 101", "c=IN IP4 0.0.0.0", "a=ice-ufrag:abcd", "a=ice-pwd:pwd", "a=fingerprint:sha-256 AA", "a=setup:active", "a=mid:0",
            "a=rtpmap:111 opus/48000/2", "a=rtpmap:101 telephone-event/8000", "a=fmtp:101 0-16", "a=extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level", "a=sendrecv",
            "m=video 9 UDP/TLS/RTP/SAVPF 96", "a=mid:1", "a=rtpmap:96 VP8/90000", "a=sendonly",
            "m=video 9 UDP/TLS/RTP/SAVPF 96", "a=mid:2", "a=rtpmap:96 VP8/90000", "a=recvonly",
            "m=audio 9 UDP/TLS/RTP/SAVPF 111", "a=mid:3", "a=rtpmap:111 opus/48000/2", "a=inactive",
            "m=video 9 UDP/TLS/RTP/SAVPF 96", "a=mid:4", "a=rtpmap:96 VP8/90000", "a=inactive",
            "m=video 9 UDP/TLS/RTP/SAVPF 96", "a=mid:5", "a=rtpmap:96 VP8/90000", "a=recvonly",
            "m=audio 9 UDP/TLS/RTP/SAVPF 111", "a=mid:6", "a=rtpmap:111 opus/48000/2", "a=recvonly",
            "m=video 9 UDP/TLS/RTP/SAVPF 96", "a=mid:7", "a=rtpmap:96 VP8/90000", "a=recvonly",
        }) + "\r\n";
        var answer = Sdp.NormalizeAnswer(offer, raw, camera: true);
        Assert.Null(Sdp.CheckAnswerShape(answer, 2));
        var sections = Sdp.Parse(answer).Sections;
        Assert.Equal(
            new[] { "sendonly", "sendonly", "inactive", "recvonly", "recvonly", "inactive", "inactive", "inactive", "inactive" },
            sections.Select(s => s.Direction).ToArray());
        Assert.Equal("m=audio 9 UDP/TLS/RTP/SAVPF 111", sections[0].Lines[0]);
        Assert.DoesNotContain(sections[0].Lines, l => l.Contains("telephone-event") || l.StartsWith("a=fmtp:101"));
        Assert.Contains(sections[0].Lines, l => l.Contains("ssrc-audio-level"));
        Assert.StartsWith("m=video 0 ", sections[8].Lines[0]);
        Assert.Contains("a=group:BUNDLE 0 1 2 3 4 5 6 7 8", answer);
        Assert.Single(answer.Split('\n'), l => l.StartsWith("a=group:BUNDLE", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------
    // Membership

    private static ParticipantState State(uint peer) => new(peer, (1000 + peer).ToString(), $"P{peer};", Role.Speaker, false, false, false, false, false);

    private static Member Member(uint peer) => new(State(peer), default, $"u{peer}");

    private static ServerMessage.RoomSnapshot Snapshot(long revision, uint own, params Member[] members) => new("1", revision, own, members.Length, members);

    private static ServerMessage.PeerJoined Joined(long revision, Member peer, int count) => new(revision, count, peer);

    private static ServerMessage.PeerLeft Left(long revision, uint peer, int count) =>
        new(revision, count, "", (1000 + peer).ToString(), peer, new SlotMapping(3, 4, 5, 0, 2));

    private static string[] Kinds(IEnumerable<MemberEvent> events) => events.Select(e => e switch
    {
        MemberEvent.Joined => "joined",
        MemberEvent.Left => "left",
        MemberEvent.Updated => "updated",
        MemberEvent.Own => "own",
        _ => "count",
    }).ToArray();

    private static uint[] JoinedIds(IEnumerable<MemberEvent> events) => events.OfType<MemberEvent.Joined>().Select(j => j.Member.State.PeerId).ToArray();

    [Fact]
    public void Snapshot_separates_self()
    {
        var m = new Membership();
        var events = m.ApplySnapshot(Snapshot(4, 1, Member(1), Member(2), Member(3)), T0);
        Assert.Equal(new[] { "own", "joined", "joined", "count" }, Kinds(events));
        Assert.Equal((2, 3, 1u), (m.Members.Count, m.ParticipantCount, m.OwnState!.PeerId));
    }

    [Fact]
    public void Deltas_are_buffered_until_the_snapshot_and_stale_ones_dropped()
    {
        var m = new Membership();
        Assert.Empty(m.ApplyJoined(Joined(3, Member(5), 0), T0));
        m.ApplyJoined(Joined(6, Member(6), 0), T0);
        Assert.Equal(new uint[] { 5, 6 }, JoinedIds(m.ApplySnapshot(Snapshot(5, 1, Member(1), Member(5)), T0)));

        m = new Membership();
        m.ApplySnapshot(Snapshot(10, 1, Member(1)), T0);
        Assert.Empty(m.ApplyJoined(Joined(10, Member(2), 0), T0));
        Assert.NotEmpty(m.ApplyJoined(Joined(11, Member(2), 2), T0));
        Assert.Empty(m.ApplyLeft(Left(9, 2, 0), T0));
        var left = Assert.IsType<MemberEvent.Left>(m.ApplyLeft(Left(12, 2, 1), T0)[0]);
        Assert.Equal(((uint?)0, 2L), (left.Mapping!.Value.Slot, left.Mapping.Value.AssignmentGeneration));
    }

    [Fact]
    public void Orphan_updates_and_tombstones()
    {
        var m = new Membership();
        m.ApplySnapshot(Snapshot(1, 1, Member(1)), T0);
        Assert.Empty(m.ApplyUpdated(State(7) with { CameraActive = true }, T0));
        var joined = Assert.IsType<MemberEvent.Joined>(m.ApplyJoined(Joined(2, Member(7), 0), At(1))[0]);
        Assert.True(joined.Member.State.CameraActive);
        m.ApplyUpdated(State(8) with { CameraActive = true }, T0);
        var late = Assert.IsType<MemberEvent.Joined>(m.ApplyJoined(Joined(3, Member(8), 0), At(20))[0]);
        Assert.False(late.Member.State.CameraActive);

        m = new Membership();
        m.ApplySnapshot(Snapshot(1, 1, Member(1), Member(2)), T0);
        m.ApplyLeft(Left(2, 2, 1), T0);
        Assert.Empty(m.ApplyUpdated(State(2) with { IsMuted = true }, T0));
    }

    [Fact]
    public void Tombstones_are_pruned()
    {
        var m = new Membership();
        m.ApplySnapshot(Snapshot(1, 1, Member(1)), T0);
        for (uint i = 0; i < 50; i++)
        {
            m.ApplyJoined(Joined(2 + 2 * i, Member(100 + i), 2), At(i));
            m.ApplyLeft(Left(3 + 2 * i, 100 + i, 1), At(i));
        }

        Assert.True(m.TombstoneCount <= 31, $"{m.TombstoneCount} tombstones kept");
        m.ApplyUpdated(State(999), At(200));
        Assert.Equal(0, m.TombstoneCount);
    }

    [Fact]
    public void Updates_and_own_state()
    {
        var m = new Membership();
        m.ApplySnapshot(Snapshot(1, 1, Member(1), Member(2)), T0);
        Assert.Empty(m.ApplyUpdated(State(2), T0));
        var updated = Assert.IsType<MemberEvent.Updated>(m.ApplyUpdated(State(2) with { IsMuted = true }, T0)[0]);
        Assert.True(updated.Member.State.IsMuted && !updated.Previous.State.IsMuted);
        Assert.True(Assert.IsType<MemberEvent.Own>(m.ApplyUpdated(State(1) with { IsMuted = true }, T0)[0]).State.IsMuted);

        m = new Membership();
        m.SetSelfPeerId(9);
        Assert.IsType<MemberEvent.Own>(m.ApplyUpdated(State(9), T0)[0]);
    }

    [Fact]
    public void Reconnect_diffs_against_previous_members()
    {
        var m = new Membership();
        m.ApplySnapshot(Snapshot(5, 1, Member(1), Member(2), Member(3)), T0);
        m.ResetSession();
        var events = m.ApplySnapshot(Snapshot(2, 9, Member(9), Member(2), Member(4)), T0);
        Assert.Equal(new uint[] { 4 }, JoinedIds(events));
        Assert.Contains(events, e => e is MemberEvent.Left l && l.Member.State.PeerId == 3);
        Assert.Equal(2, m.Clear().Count);
    }

    [Fact]
    public void A_new_session_forgets_the_mappings_of_the_previous_one()
    {
        var m = new Membership();
        m.ApplySnapshot(Snapshot(5, 1, Member(1), Member(2) with { Mapping = new SlotMapping(3, 4, 5, 0, 4) }, Member(3) with { Mapping = new SlotMapping(6, 7, 8, 1, 9) }), T0);
        m.ResetSession();
        Assert.All(m.Members.Values, member => Assert.Null(member.Mapping));
        var events = m.ApplySnapshot(Snapshot(1, 9, Member(9), Member(2) with { Mapping = new SlotMapping(3, 4, 5, 0, 1) }), T0);
        var left = Assert.Single(events.OfType<MemberEvent.Left>());
        Assert.Equal((3u, (SlotMapping?)null), (left.Member.State.PeerId, left.Mapping));
        Assert.Equal(1, m.Members[2].Mapping!.Value.AssignmentGeneration);
    }

    [Fact]
    public void Mapping_is_kept_when_an_update_has_none()
    {
        var m = new Membership();
        var mapped = Member(2) with { Mapping = new SlotMapping(6, 0, 0, 1, 3) };
        m.ApplySnapshot(Snapshot(1, 1, Member(1), mapped), T0);
        m.ApplyUpdated(State(2) with { IsMuted = true }, T0);
        Assert.Equal(3, m.Members[2].Mapping!.Value.AssignmentGeneration);
    }

    // -------------------------------------------------------------------------------------------
    // Tracks

    private static SlotOccupant Occ(uint peer, long generation) => new(peer, (100 + peer).ToString(), generation, true, false, false);

    [Fact]
    public void Slot_map_generations()
    {
        var slots = new SlotMap();
        Assert.Equal(SlotChange.Assigned, slots.Assign(0, Occ(1, 1)));
        Assert.Equal(SlotChange.Flags, slots.Assign(0, Occ(1, 1) with { VideoActive = true }));
        Assert.Equal(SlotChange.Replaced, slots.Assign(0, Occ(2, 2)));
        Assert.Equal(SlotChange.Ignored, slots.Assign(0, Occ(1, 1)));
        Assert.Equal(SlotChange.Ignored, slots.Assign(0, Occ(3, 0)));

        slots = new SlotMap();
        slots.Assign(1, Occ(1, 5));
        Assert.Null(slots.Release(1, 4));
        Assert.Equal(1u, slots.Release(1, 5)!.PeerId);
        Assert.Equal(SlotChange.Ignored, slots.Assign(1, Occ(1, 5)));
        Assert.Equal(SlotChange.Assigned, slots.Assign(1, Occ(2, 6)));

        slots = new SlotMap();
        slots.Assign(0, Occ(1, 2));
        Assert.Equal(1u, slots.Release(0, 3)!.PeerId);

        slots = new SlotMap();
        slots.Assign(0, Occ(1, 1));
        slots.Assign(1, Occ(1, 2));
        slots.Assign(2, Occ(2, 1));
        Assert.Equal(new uint[] { 0, 1 }, slots.ReleasePeer(1));
        Assert.Equal(SlotChange.Ignored, slots.Assign(0, Occ(1, 1)));
    }

    private static OfferLayout Layout(params FixtureSlot[] slots) => Sdp.AnalyzeOffer(OfferFixtures.BuildOffer(new OfferOptions(Slots: slots)));

    [Fact]
    public void Dynamic_occupancy()
    {
        var d = new DynamicOccupancy();
        d.ApplyOffer(Layout(FixtureSlot.Peer("11", 1), FixtureSlot.Peer("12", 2)), 1, T0);
        Assert.Equal(new DynamicOccupant(1, "11", 1), d.Get(3));
        Assert.Equal(2u, d.Get(7)!.PeerId);
        Assert.Null(d.Get(5));
        d.ApplyOffer(Layout(FixtureSlot.Peer("11", 1), FixtureSlot.Peer("13", 3)), 2, T0);
        Assert.Equal(1L, d.Get(3)!.Epoch);
        Assert.Equal(new DynamicOccupant(3, "13", 2), d.Get(6));

        d = new DynamicOccupancy();
        var offer = Layout(FixtureSlot.Peer("11", 1));
        d.ApplyOffer(offer, 1, T0);
        d.RemovePeer(1, T0, TimeSpan.FromSeconds(30));
        Assert.Null(d.Get(3));
        d.ApplyOffer(offer, 2, At(1));
        Assert.Null(d.Get(3));
        d.ApplyOffer(offer, 3, At(31));
        Assert.Equal(1u, d.Get(3)!.PeerId);
    }

    [Fact]
    public void Departures_are_pruned_without_offers()
    {
        // Fixed-slot rooms never apply offers to the dynamic occupancy: departures must expire on removal too.
        var d = new DynamicOccupancy();
        for (uint i = 0; i < 100; i++)
        {
            d.RemovePeer(i, At(i), TimeSpan.FromSeconds(30));
        }

        Assert.True(d.DepartedCount <= 31, $"{d.DepartedCount} departures kept");
    }

    private static (uint, TrackSource, uint, bool, long)[] Describe(SortedDictionary<uint, TrackSpec> specs) =>
        specs.Values.Select(s => (s.Mid, s.Source, s.PeerId, s.Muted, s.Epoch)).ToArray();

    [Fact]
    public void Desired_tracks_and_their_diff()
    {
        var slots = new SlotMap();
        slots.Assign(0, Occ(1, 1) with { AudioActive = false, VideoActive = true });
        slots.Assign(1, Occ(2, 1) with { ScreenActive = true });
        slots.Assign(5, Occ(3, 1));
        Assert.Equal(
            new[] { (3u, TrackSource.Microphone, 1u, true, 1L), (4u, TrackSource.Camera, 1u, false, 1L), (6u, TrackSource.Microphone, 2u, false, 1L), (8u, TrackSource.Screen, 2u, false, 1L) },
            Describe(Tracks.Desired(DownlinkMode.Fixed, 2, slots, new DynamicOccupancy())));

        slots = new SlotMap();
        slots.Assign(0, Occ(1, 1));
        var before = Tracks.Desired(DownlinkMode.Fixed, 2, slots, new DynamicOccupancy());
        slots.Assign(0, Occ(1, 1) with { AudioActive = false });
        var muted = Tracks.Desired(DownlinkMode.Fixed, 2, slots, new DynamicOccupancy());
        var diff = Tracks.Compare(before, muted);
        Assert.True(diff.Unsubscribed.Count == 0 && diff.Subscribed.Count == 0 && diff.MuteChanged[0].Mid == 3);
        slots.Assign(0, Occ(2, 2));
        diff = Tracks.Compare(muted, Tracks.Desired(DownlinkMode.Fixed, 2, slots, new DynamicOccupancy()));
        Assert.Equal((1u, 2u), (diff.Unsubscribed[0].PeerId, diff.Subscribed[0].PeerId));

        var d = new DynamicOccupancy();
        d.ApplyOffer(Layout(FixtureSlot.Peer("11", 1) with { Screen = true }), 4, T0);
        Assert.Equal(
            new[] { (3u, TrackSource.Microphone, 1u, false, 4L), (4u, TrackSource.Camera, 1u, false, 4L), (5u, TrackSource.Screen, 1u, false, 4L) },
            Describe(Tracks.Desired(DownlinkMode.Dynamic, 1, new SlotMap(), d)));
    }

    [Fact]
    public void Occupant_from_a_membership_mapping()
    {
        var state = ParticipantState.Empty(4) with { IsMuted = true, CameraActive = true };
        var (slot, occupant) = Tracks.OccupantFromMapping(4, "104", new SlotMapping(6, 0, 0, 1, 3), state)!.Value;
        Assert.Equal((1u, 3L, false, true), (slot, occupant.Generation, occupant.AudioActive, occupant.VideoActive));
        Assert.Null(Tracks.OccupantFromMapping(4, "104", default(SlotMapping), state));
        Assert.Null(Tracks.OccupantFromMapping(4, "104", null, state));
    }

    // -------------------------------------------------------------------------------------------
    // Control

    [Fact]
    public void Backoff_is_equal_jitter_and_capped()
    {
        foreach (var (attempt, low, high) in new[] { (0, 500, 1000), (1, 1000, 2000), (3, 4000, 8000), (9, 4000, 8000) })
        {
            Assert.Equal(TimeSpan.FromMilliseconds(low), ReconnectPolicy.DefaultDelay(attempt, 0));
            Assert.Equal(TimeSpan.FromMilliseconds(high), ReconnectPolicy.DefaultDelay(attempt, 1));
        }
    }

    private static TimeSpan Second(int _) => TimeSpan.FromSeconds(1);

    [Fact]
    public void Reconnect_policy()
    {
        var p = new ReconnectPolicy(new ReconnectLimits());
        foreach (var (code, reason) in new[]
                 {
                     (1000, StopReason.ServerClosed), (4006, StopReason.Kicked), (4011, StopReason.AloneTimeout), (4012, StopReason.DuplicateSession),
                     (4003, StopReason.ServerMisconfigured),
                 })
        {
            Assert.Equal(ReconnectDecision.Halt(reason), p.Next(code, false, T0, Second));
        }

        Assert.Equal(ReconnectDecision.Halt(StopReason.Client), p.Next(1006, true, T0, Second));

        p = new ReconnectPolicy(new ReconnectLimits());
        for (var i = 0; i < 2; i++)
        {
            var d = p.Next(4005, false, T0, Second);
            Assert.True(d.Reconnect && d.RefreshToken);
        }

        Assert.Equal(ReconnectDecision.Halt(StopReason.AuthFailed), p.Next(4005, false, T0, Second));

        p = new ReconnectPolicy(new ReconnectLimits());
        Assert.True(p.Next(1002, false, T0, Second).Reconnect);
        Assert.Equal(ReconnectDecision.Halt(StopReason.ProtocolRejected), p.Next(1008, false, T0, Second));

        p = new ReconnectPolicy(new ReconnectLimits(MaxAttempts: 2));
        p.Next(1006, false, T0, Second);
        p.Next(null, false, T0, Second);
        Assert.Equal(ReconnectDecision.Halt(StopReason.Exhausted), p.Next(1006, false, T0, Second));
        p.Reset();
        Assert.True(p.Next(1006, false, T0, Second).Reconnect);

        p = new ReconnectPolicy(new ReconnectLimits(MaxDuration: TimeSpan.FromSeconds(60)));
        p.Next(1006, false, T0, Second);
        Assert.Equal(ReconnectDecision.Halt(StopReason.Exhausted), p.Next(1006, false, At(120), Second));
        p = new ReconnectPolicy(new ReconnectLimits(Enabled: false));
        Assert.Equal(ReconnectDecision.Halt(StopReason.Disabled), p.Next(1006, false, T0, Second));
    }

    [Fact]
    public void Huge_durations_do_not_overflow_deadlines()
    {
        Assert.Equal(TimeSpan.MaxValue, Clock.Deadline(T0, TimeSpan.MaxValue));
        Assert.Equal(TimeSpan.MaxValue, Clock.Deadline(T0, Timeout.InfiniteTimeSpan));
        Assert.Equal(TimeSpan.MaxValue, Clock.Deadline(TimeSpan.MaxValue - TimeSpan.FromTicks(1), TimeSpan.FromSeconds(1)));
        Assert.Equal(At(5), Clock.Deadline(T0, TimeSpan.FromSeconds(5)));
        Assert.Equal(Timeout.InfiniteTimeSpan, Clock.Delay(TimeSpan.MaxValue));
        Assert.Equal(TimeSpan.Zero, Clock.Wait(TimeSpan.FromSeconds(-1)));
        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue - 1), Clock.Wait(TimeSpan.MaxValue));
        var p = new ReconnectPolicy(new ReconnectLimits(MaxDuration: TimeSpan.MaxValue));
        Assert.True(p.Next(1006, false, TimeSpan.MaxValue, Second).Reconnect);
        var m = new Membership();
        m.ApplySnapshot(Snapshot(1, 1, Member(1), Member(2)), TimeSpan.MaxValue - TimeSpan.FromSeconds(1));
        m.ApplyLeft(Left(2, 2, 1), TimeSpan.MaxValue - TimeSpan.FromSeconds(1));
        var d = new DynamicOccupancy();
        d.RemovePeer(1, TimeSpan.MaxValue, TimeSpan.MaxValue);
        var mute = new MuteSync();
        mute.ObserveServerState(true, false, TimeSpan.MaxValue);
        Assert.False(mute.TakeForcedMute(TimeSpan.MaxValue - TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void Mute_sync()
    {
        var m = new MuteSync();
        Assert.Equal((1L, 2L), (m.Next(false), m.Next(true)));
        Assert.True(m.Acknowledge(true, 2) && m.PendingCount == 0);
        m.Next(false);
        Assert.True(!m.Acknowledge(true, null) && m.Acknowledge(false, null));
        m.Next(true);
        m.Reject();
        Assert.Equal(0, m.PendingCount);

        m.ObserveServerState(true, false, T0);
        Assert.False(m.TakeForcedMute(T0 + TimeSpan.FromMilliseconds(100)));
        Assert.True(m.TakeForcedMute(T0 + TimeSpan.FromMilliseconds(400)));
        Assert.False(m.TakeForcedMute(At(1)));
        m.Next(false);
        m.ObserveServerState(true, false, T0);
        Assert.False(m.TakeForcedMute(At(1)));
        m.Acknowledge(false, null);
        m.ObserveServerState(true, true, T0);
        Assert.False(m.TakeForcedMute(At(1)));
    }

    [Fact]
    public void Speaker_detector()
    {
        TimeSpan Ms(int ms) => T0 + TimeSpan.FromMilliseconds(ms);
        var d = new SpeakerDetector(new SpeakerOptions());
        static Dictionary<uint, double> Levels(params (uint, double)[] pairs) => pairs.ToDictionary(p => p.Item1, p => p.Item2);
        Assert.Empty(d.Update(Levels((1, 0.5)), Ms(0)));
        Assert.Equal(new uint[] { 1 }, d.Update(Levels((1, 0.5)), Ms(200)));
        Assert.Equal(new uint[] { 1 }, d.Update(Levels((1, 0.0)), Ms(1000)));
        Assert.Empty(d.Update(Levels((1, 0.0)), Ms(1800)));
        d.Update(Levels((1, 0.3), (2, 0.9)), Ms(2000));
        Assert.Equal(new uint[] { 2, 1 }, d.Update(Levels((1, 0.3), (2, 0.9)), Ms(2200)));
        Assert.Equal(new uint[] { 2 }, d.Update(Levels((2, 0.9)), Ms(2400)));
        Assert.True(d.IsSpeaking(2) && !d.IsSpeaking(1));
    }
}
