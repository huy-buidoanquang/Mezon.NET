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
/// <param name="Pool">The pools of a decoupled room, whose remote m-lines replace <paramref name="Slots"/>.</param>
internal sealed record OfferOptions(bool Audience = false, string Screen = "vp8", IReadOnlyList<FixtureSlot>? Slots = null, MediaPoolLayout? Pool = null)
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

    private const string TransportCc = "a=extmap:6 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01";
    private const string MidExtension = "a=extmap:7 urn:ietf:params:rtp-hdrext:sdes:mid";

    private static void Audio(List<string> lines, uint mid, string direction, string? msid, bool ssrc = true, string? track = null)
    {
        lines.Add("m=audio 7000 UDP/TLS/RTP/SAVPF 111");
        lines.AddRange(Transport);
        lines.Add($"a={direction}");
        lines.Add($"a=mid:{mid}");
        lines.Add("a=rtcp-mux");
        if (mid == Sdp.MidMicrophone)
        {
            lines.Add(TransportCc);
            lines.Add(MidExtension);
            lines.Add("a=extmap:8 urn:ietf:params:rtp-hdrext:ssrc-audio-level");
        }
        else
        {
            lines.Add(MidExtension);
        }

        lines.Add("a=rtpmap:111 opus/48000/2");
        lines.Add("a=fmtp:111 minptime=10;useinbandfec=1;usedtx=1;maxaveragebitrate=32000");
        if (msid is not null)
        {
            lines.Add($"a=msid:{msid} {track ?? "audio-" + msid}");
            if (ssrc)
            {
                lines.Add($"a=ssrc:{1000 + mid} cname:{msid}");
            }
        }
    }

    private static void Video(List<string> lines, uint mid, string direction, string codecs, string? msid, string prefix, bool ssrc = true, string? track = null)
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
        if (mid < Sdp.RemoteMidBase)
        {
            lines.Add(TransportCc);
            lines.Add(MidExtension);
        }
        else
        {
            lines.Add(MidExtension);
            if (direction == "sendonly")
            {
                lines.Add(TransportCc);
            }
        }

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
            lines.Add($"a=msid:{msid} {track ?? prefix + "-" + msid}");
            if (ssrc)
            {
                lines.Add($"a=ssrc:{2000 + mid} cname:{msid}");
            }
        }
    }

    /// <summary>Builds an offer.</summary>
    public static string BuildOffer(OfferOptions options)
    {
        var slots = options.Pool is null ? options.SlotList : new List<FixtureSlot>();
        var mids = string.Join(" ", Enumerable.Range(0, (int)(options.Pool?.MLines ?? Sdp.RemoteMidBase + Sdp.MidsPerSlot * slots.Count)));
        var lines = new List<string> { "v=0", "o=- 1 1 IN IP4 127.0.0.1", "s=-", "t=0 0", $"a=group:BUNDLE {mids}", "a=ice-lite" };
        var uplink = options.Audience ? "inactive" : "recvonly";
        Audio(lines, 0, "recvonly", null);
        Video(lines, 1, uplink, "vp8", null, "video");
        Video(lines, 2, uplink, options.Screen, null, "video");
        if (options.Pool is { } pool)
        {
            // mezon-sfu append_fixed_decoupled_slot_audio/_video: every pool m-line is sendonly, msids name the slot.
            for (var n = 0u; n < pool.Audio; n++)
            {
                Audio(lines, pool.Mid(TrackSource.Microphone, n)!.Value, "sendonly", $"audio-slot-{n}", track: $"audio-slot-{n}");
            }

            for (var n = 0u; n < pool.Camera; n++)
            {
                Video(lines, pool.Mid(TrackSource.Camera, n)!.Value, "sendonly", "vp8", $"camera-slot-{n}", "video", track: $"video-slot-{n}");
            }

            for (var n = 0u; n < pool.Screen; n++)
            {
                Video(lines, pool.Mid(TrackSource.Screen, n)!.Value, "sendonly", options.Screen, $"screen-slot-{n}", "screen", track: $"screen-slot-{n}");
            }
        }

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
