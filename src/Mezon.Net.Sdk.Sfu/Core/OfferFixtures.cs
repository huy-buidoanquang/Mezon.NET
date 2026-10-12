using System.Collections.Generic;
using System.Linq;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>Occupant of a remote slot in a built offer: a peer (dynamic) or a slot number (fixed).</summary>
internal sealed record FixtureSlot(Occupant? Occupant = null, bool NoCamera = false, bool Screen = false, bool NoSsrc = false)
{
    public static FixtureSlot Peer(string userId, uint peerId) => new(new Occupant.Peer(userId, peerId));

    public static FixtureSlot Fixed(uint slot) => new(new Occupant.Slot(slot));

    /// <summary>An inactive slot.</summary>
    public static FixtureSlot Empty => new();
}

/// <summary>Options of a built offer.</summary>
/// <param name="Screen">"vp8" (default), "vp9" or "vp9+vp8".</param>
internal sealed record OfferOptions(bool Audience = false, string Screen = "vp8", IReadOnlyList<FixtureSlot>? Slots = null)
{
    public IReadOnlyList<FixtureSlot> SlotList => Slots ?? new List<FixtureSlot>();
}

/// <summary>Synthetic offers shaped like mezon-sfu's, for tests of the core and of the WebRTC adapter.</summary>
internal static class OfferFixtures
{
    private static readonly string[] Transport =
    {
        "c=IN IP4 127.0.0.1",
        "a=ice-ufrag:srvufrag",
        "a=ice-pwd:srvpwdsrvpwdsrvpwdsrvpwd",
        "a=fingerprint:sha-256 AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99",
        "a=setup:passive",
        "a=candidate:1 1 udp 2130706431 127.0.0.1 7000 typ host",
    };

    private static string? Msid(Occupant? occupant) => occupant switch
    {
        Occupant.Peer p => $"u{p.UserId}-p{p.PeerId}",
        Occupant.Slot s => $"slot-{s.Number}",
        _ => null,
    };

    private static void Audio(List<string> lines, uint mid, string direction, string? msid, bool ssrc = true)
    {
        lines.Add("m=audio 7000 UDP/TLS/RTP/SAVPF 111");
        lines.AddRange(Transport);
        lines.Add($"a={direction}");
        lines.Add($"a=mid:{mid}");
        lines.Add("a=rtcp-mux");
        lines.Add("a=extmap:8 urn:ietf:params:rtp-hdrext:ssrc-audio-level");
        lines.Add("a=extmap:7 urn:ietf:params:rtp-hdrext:sdes:mid");
        lines.Add("a=rtpmap:111 opus/48000/2");
        lines.Add("a=fmtp:111 minptime=10;useinbandfec=1;usedtx=1;maxaveragebitrate=32000");
        if (msid is not null)
        {
            lines.Add($"a=msid:{msid} audio-{msid}");
            if (ssrc)
            {
                lines.Add($"a=ssrc:{1000 + mid} cname:{msid}");
            }
        }
    }

    private static void Video(List<string> lines, uint mid, string direction, string codecs, string? msid, string prefix, bool ssrc = true)
    {
        var pts = codecs switch
        {
            "vp9" => "98 99",
            "vp9+vp8" => "98 99 96 97",
            _ => "96 97",
        };
        lines.Add($"m=video 7000 UDP/TLS/RTP/SAVPF {pts}");
        lines.AddRange(Transport);
        lines.Add($"a={direction}");
        lines.Add($"a=mid:{mid}");
        lines.Add("a=rtcp-mux");
        lines.Add("a=extmap:7 urn:ietf:params:rtp-hdrext:sdes:mid");
        if (codecs != "vp8")
        {
            lines.AddRange(new[] { "a=rtpmap:98 VP9/90000", "a=fmtp:98 profile-id=0", "a=rtcp-fb:98 nack pli", "a=rtpmap:99 rtx/90000", "a=fmtp:99 apt=98" });
        }

        if (codecs != "vp9")
        {
            lines.AddRange(new[] { "a=rtpmap:96 VP8/90000", "a=rtcp-fb:96 nack pli", "a=rtpmap:97 rtx/90000", "a=fmtp:97 apt=96" });
        }

        if (msid is not null)
        {
            lines.Add($"a=msid:{msid} {prefix}-{msid}");
            if (ssrc)
            {
                lines.Add($"a=ssrc:{2000 + mid} cname:{msid}");
            }
        }
    }

    /// <summary>Builds an offer.</summary>
    public static string BuildOffer(OfferOptions options)
    {
        var slots = options.SlotList;
        var mids = "0 1 2" + string.Concat(Enumerable.Range(0, slots.Count).Select(k => $" {3 + 3 * k} {4 + 3 * k} {5 + 3 * k}"));
        var lines = new List<string> { "v=0", "o=- 1 1 IN IP4 127.0.0.1", "s=-", "t=0 0", $"a=group:BUNDLE {mids}", "a=ice-lite" };
        var uplink = options.Audience ? "inactive" : "recvonly";
        Audio(lines, 0, "recvonly", null);
        Video(lines, 1, uplink, "vp8", null, "video");
        Video(lines, 2, uplink, options.Screen, null, "video");
        for (var k = 0u; k < slots.Count; k++)
        {
            var slot = slots[(int)k];
            var msid = Msid(slot.Occupant);
            var live = slot.Occupant is not null;
            static string Dir(bool on) => on ? "sendonly" : "inactive";
            Audio(lines, 3 + 3 * k, Dir(live), msid);
            Video(lines, 4 + 3 * k, Dir(live && !slot.NoCamera), "vp8", msid, "video", !slot.NoSsrc);
            Video(lines, 5 + 3 * k, Dir(live && slot.Screen), options.Screen, msid, "screen", !slot.NoSsrc);
        }

        return string.Join("\r\n", lines) + "\r\n";
    }
}
