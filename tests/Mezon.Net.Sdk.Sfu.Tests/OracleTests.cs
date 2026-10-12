using System.Net;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;

namespace Mezon.Net.Sdk.Sfu.Tests;

/// <summary>
/// Answer-shape oracle: SIPSorcery answers (through the adapter) to mezon-sfu-shaped offers must pass the server's
/// <c>validate_answer_mline_shape</c> and keep the uplink directions the protocol needs.
/// </summary>
public class OracleTests
{
    private static Task<IPeerConnection> Peer(bool camera) =>
        SipsorceryPeer.CreateAsync(new PeerConfig { BindAddress = IPAddress.Loopback, Camera = camera }, CancellationToken.None);

    private static async Task<string> Negotiate(IPeerConnection pc, string offer)
    {
        await pc.SetRemoteOfferAsync(offer);
        var answer = await pc.CreateAnswerAsync();
        await pc.SetLocalAnswerAsync(answer);
        return pc.LocalSdp;
    }

    private static string?[] Directions(string answer) => Sdp.Parse(answer).Sections.Select(s => s.Direction).ToArray();

    private static FixtureSlot[] DynamicSlots(int n) =>
        Enumerable.Range(0, n).Select(k => FixtureSlot.Peer((9000 + k).ToString(), (uint)(100 + k)) with { Screen = k % 2 == 0 }).ToArray();

    private static readonly Dictionary<string, (bool Speaker, OfferOptions Options)> CaseTable = new()
    {
        ["speaker alone"] = (true, new OfferOptions()),
        ["audience alone"] = (false, new OfferOptions(Audience: true)),
        ["speaker, 1 dynamic slot"] = (true, new OfferOptions(Slots: DynamicSlots(1))),
        ["speaker, 5 dynamic slots, one inactive"] = (true, new OfferOptions(Slots: DynamicSlots(4).Append(FixtureSlot.Empty).ToArray())),
        ["audience, 20 dynamic slots"] = (false, new OfferOptions(Audience: true, Slots: DynamicSlots(20))),
        ["speaker, fixed 2"] = (true, new OfferOptions(Slots: new[] { FixtureSlot.Fixed(0), FixtureSlot.Fixed(1) })),
        ["speaker, vp9 screen"] = (true, new OfferOptions(Screen: "vp9", Slots: DynamicSlots(2))),
        ["speaker, vp9+vp8 screen"] = (true, new OfferOptions(Screen: "vp9+vp8", Slots: DynamicSlots(2))),
    };

    public static IEnumerable<object[]> Cases() => CaseTable.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_shape_oracle(string name)
    {
        var (speaker, options) = CaseTable[name];
        var pc = await Peer(speaker);
        try
        {
            var answer = await Negotiate(pc, OfferFixtures.BuildOffer(options));
            var problem = Sdp.CheckAnswerShape(answer, (uint)options.SlotList.Count);
            Assert.True(problem is null, $"{name}: {problem}\n{answer}");
            var dirs = Directions(answer);
            Assert.Equal("sendonly", dirs[0]);
            Assert.Equal(speaker ? "sendonly" : "inactive", dirs[1]);
            Assert.Equal("inactive", dirs[2]);
            for (var k = 0; k < options.SlotList.Count; k++)
            {
                var slot = options.SlotList[k];
                Assert.Equal(slot.Occupant is not null ? "recvonly" : "inactive", dirs[3 + 3 * k]);
                Assert.Equal(slot.Occupant is not null ? "recvonly" : "inactive", dirs[4 + 3 * k]);
                Assert.Equal(slot.Occupant is not null && slot.Screen ? "recvonly" : "inactive", dirs[5 + 3 * k]);
            }

            var sections = Sdp.Parse(answer).Sections;
            Assert.Equal(
                "a=extmap:8 urn:ietf:params:rtp-hdrext:ssrc-audio-level",
                Assert.Single(sections[0].Lines, l => l.StartsWith("a=extmap:", StringComparison.Ordinal)));
            Assert.Equal(8, ((SipsorceryPeer)pc).MicrophoneAudioLevelId);
            Assert.All(sections, s => Assert.Contains("UDP/TLS/RTP/SAVPF", s.Lines[0]));
            Assert.Equal("m=audio 9 UDP/TLS/RTP/SAVPF 111", sections[0].Lines[0]);
            Assert.Contains(" 96", sections[1].Lines[0]);
            var (microphone, camera) = pc.Uplinks();
            Assert.NotNull(microphone);
            Assert.Equal(speaker, camera is not null);
        }
        finally
        {
            await pc.CloseAsync();
        }
    }

    [Fact]
    public async Task Renegotiation_keeps_the_ufrag_and_reuses_mids()
    {
        var pc = await Peer(true);
        try
        {
            var first = await Negotiate(pc, OfferFixtures.BuildOffer(new OfferOptions(Slots: DynamicSlots(1))));
            var ufrag = Sdp.Parse(first).IceUfrag;
            Assert.NotNull(ufrag);
            var steps = new[]
            {
                DynamicSlots(3),
                new[] { FixtureSlot.Peer("9000", 100), FixtureSlot.Empty, FixtureSlot.Peer("9002", 102) },
                new[] { FixtureSlot.Peer("9000", 100), FixtureSlot.Peer("7777", 300), FixtureSlot.Peer("9002", 102) },
            };
            for (var i = 0; i < steps.Length; i++)
            {
                var slots = steps[i];
                var answer = await Negotiate(pc, OfferFixtures.BuildOffer(new OfferOptions(Slots: slots)));
                var problem = Sdp.CheckAnswerShape(answer, (uint)slots.Length);
                Assert.True(problem is null, $"step {i}: {problem}\n{answer}");
                Assert.Equal(ufrag, Sdp.Parse(answer).IceUfrag);
                var dirs = Directions(answer);
                Assert.Equal(("sendonly", "sendonly"), (dirs[0], dirs[1]));
                var receiving = pc.ReceivingMids();
                for (var k = 0; k < slots.Length; k++)
                {
                    Assert.Equal(slots[k].Occupant is not null ? "recvonly" : "inactive", dirs[3 + 3 * k]);
                    Assert.Equal(slots[k].Occupant is not null, receiving.Contains((uint)(3 + 3 * k)));
                }
            }
        }
        finally
        {
            await pc.CloseAsync();
        }
    }

    [Fact]
    public async Task Accepts_publishers_announced_before_their_ssrc()
    {
        // mezon-sfu writes a=msid on a live camera m-line before it knows the camera's SSRC.
        var offer = OfferFixtures.BuildOffer(new OfferOptions(Slots: DynamicSlots(2).Select(s => s with { NoSsrc = true }).ToArray()));
        Assert.Contains("a=msid:u9000-p100 video-u9000-p100", offer);
        Assert.DoesNotContain("a=ssrc:2004", offer);
        var pc = await Peer(true);
        try
        {
            var answer = await Negotiate(pc, offer);
            Assert.Null(Sdp.CheckAnswerShape(answer, 2));
            Assert.Contains(3u, pc.ReceivingMids());
        }
        finally
        {
            await pc.CloseAsync();
        }
    }
}
