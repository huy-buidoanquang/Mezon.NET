using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Mezon.Net.Sdk.Sfu.Core;

// Reading the mezon-sfu offer and answer shapes.
//
// Fixed mid layout of every mezon-sfu offer (mezon-sfu/include/sfu/datadef.h, src/protocol/signaling/sdp.c):
// mids 0/1/2 are this client's uplinks (microphone, camera, screen); from mid 3 on, each remote slot k owns
// mids 3+3k (audio), 4+3k (camera), 5+3k (screen). Decoupled rooms (MediaPoolLayout) instead list every audio
// slot, then every camera slot, then every screen slot.

/// <summary>Who a remote m-line carries, from its <c>a=msid</c> line.</summary>
internal abstract record Occupant
{
    /// <summary>Dynamic downlink: <c>a=msid:u&lt;user&gt;-p&lt;peer&gt; &lt;kind&gt;-u&lt;user&gt;-p&lt;peer&gt;</c>.</summary>
    public sealed record Peer(string UserId, uint PeerId) : Occupant;

    /// <summary>Fixed-slot downlink: <c>a=msid:slot-&lt;n&gt;</c>; the publisher comes from slot events.</summary>
    public sealed record Slot(uint Number) : Occupant;
}

/// <summary>One media section.</summary>
internal sealed class SdpSection
{
    /// <summary><c>audio</c> or <c>video</c> (from the <c>m=</c> line).</summary>
    public string Media { get; set; } = "";

    /// <summary>Port in the <c>m=</c> line; 0 means rejected.</summary>
    public int Port { get; set; }

    public string? Mid { get; set; }

    public string? Direction { get; set; }

    /// <summary>Payload type → upper-case codec name (VP8, RTX, OPUS).</summary>
    public SortedDictionary<int, string> Codecs { get; } = new();

    public Occupant? Occupant { get; set; }

    /// <summary>SSRCs announced with <c>a=ssrc</c>.</summary>
    public List<uint> Ssrcs { get; } = new();

    /// <summary>Raw lines of the section, the <c>m=</c> line first.</summary>
    public List<string> Lines { get; } = new();
}

/// <summary>A split SDP.</summary>
internal sealed class ParsedSdp
{
    public List<string> SessionLines { get; } = new();

    public List<SdpSection> Sections { get; } = new();

    /// <summary>The first <c>a=ice-ufrag</c> (session or media level).</summary>
    public string? IceUfrag { get; set; }
}

/// <summary>Slot and source of a remote mid.</summary>
internal readonly record struct RemoteMid(uint Slot, TrackSource Source);

/// <summary>A remote m-line of an offer.</summary>
internal sealed record RemoteSection(uint Mid, uint Slot, TrackSource Source, string? Direction, Occupant? Occupant);

/// <summary>The remote-slot structure of a server offer.</summary>
internal sealed record OfferLayout(ParsedSdp Sdp, uint SlotCount, IReadOnlyList<RemoteSection> Remote, DownlinkMode Mode);

/// <summary>
/// The pools of a decoupled room (<c>joined</c> with <c>media_pool_mode: "decoupled"</c>): every offer has the three
/// uplink m-lines, then <see cref="Audio"/> audio slots, <see cref="Camera"/> camera slots and <see cref="Screen"/>
/// screen slots, one m-line each with consecutive mids (mezon-sfu <c>sfu_layout_slot_mid</c>).
/// </summary>
internal sealed record MediaPoolLayout(uint Audio, uint Camera, uint Screen)
{
    /// <summary>The number of m-lines of an offer (and of its answer).</summary>
    public long MLines => Sdp.RemoteMidBase + (long)Audio + Camera + Screen;

    /// <summary>The mid of <paramref name="slot"/> in the <paramref name="kind"/> pool, or null when the pool has no such slot.</summary>
    public uint? Mid(TrackSource kind, uint slot)
    {
        long start = Sdp.RemoteMidBase;
        long count = Audio;
        if (kind != TrackSource.Microphone)
        {
            start += Audio;
            count = Camera;
        }

        if (kind == TrackSource.Screen)
        {
            start += Camera;
            count = Screen;
        }

        var mid = start + slot;
        return slot < count && mid <= uint.MaxValue ? (uint)mid : null;
    }
}

internal static class Sdp
{
    public const uint MidMicrophone = 0;
    public const uint MidCamera = 1;
    public const uint MidScreen = 2;
    public const uint RemoteMidBase = 3;
    public const uint MidsPerSlot = 3;

    private static readonly string[] Directions = { "sendonly", "recvonly", "sendrecv", "inactive" };

    private static string? DigitsAfter(string stream, char marker)
    {
        foreach (var part in stream.Split('-'))
        {
            if (part.Length > 1 && part[0] == marker && part.Skip(1).All(c => c >= '0' && c <= '9'))
            {
                return part.Substring(1);
            }
        }

        return null;
    }

    internal static Occupant? ParseMsid(string value)
    {
        var stream = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (stream is null)
        {
            return null;
        }

        if (stream.StartsWith("slot-", StringComparison.Ordinal))
        {
            return uint.TryParse(stream.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? new Occupant.Slot(n) : null;
        }

        var user = DigitsAfter(stream, 'u');
        var peerText = DigitsAfter(stream, 'p');
        if (user is null || peerText is null || !uint.TryParse(peerText, NumberStyles.None, CultureInfo.InvariantCulture, out var peer) || peer == 0)
        {
            return null;
        }

        var trimmed = user.TrimStart('0');
        return new Occupant.Peer(trimmed.Length == 0 ? "0" : trimmed, peer);
    }

    public static IEnumerable<string> Lines(string sdp) =>
        sdp.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);

    /// <summary>Splits an SDP into session lines and media sections.</summary>
    public static ParsedSdp Parse(string sdp)
    {
        var parsed = new ParsedSdp();
        SdpSection? section = null;
        foreach (var line in Lines(sdp))
        {
            if (line.StartsWith("m=", StringComparison.Ordinal))
            {
                var fields = line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                section = new SdpSection
                {
                    Media = fields.Length > 0 ? fields[0] : "",
                    Port = fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : 0,
                };
                section.Lines.Add(line);
                parsed.Sections.Add(section);
                continue;
            }

            string? ufrag = line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal) ? line.Substring(12).Trim() : null;
            if (section is null)
            {
                parsed.SessionLines.Add(line);
                parsed.IceUfrag ??= ufrag;
                continue;
            }

            section.Lines.Add(line);
            if (line.StartsWith("a=mid:", StringComparison.Ordinal))
            {
                section.Mid = line.Substring(6).Trim();
            }
            else if (line.StartsWith("a=msid:", StringComparison.Ordinal))
            {
                section.Occupant ??= ParseMsid(line.Substring(7));
            }
            else if (line.StartsWith("a=rtpmap:", StringComparison.Ordinal))
            {
                var map = line.Substring(9);
                var space = map.IndexOf(' ');
                if (space > 0 && int.TryParse(map.Substring(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out var pt))
                {
                    section.Codecs[pt] = map.Substring(space + 1).Split('/')[0].ToUpperInvariant();
                }
            }
            else if (line.StartsWith("a=ssrc:", StringComparison.Ordinal))
            {
                var value = line.Substring(7);
                var end = value.IndexOf(' ');
                if (uint.TryParse(end > 0 ? value.Substring(0, end) : value, NumberStyles.None, CultureInfo.InvariantCulture, out var ssrc) && !section.Ssrcs.Contains(ssrc))
                {
                    section.Ssrcs.Add(ssrc);
                }
            }
            else if (ufrag is not null)
            {
                parsed.IceUfrag ??= ufrag;
            }
            else if (line.StartsWith("a=", StringComparison.Ordinal) && Directions.Contains(line.Substring(2).Trim()))
            {
                section.Direction = line.Substring(2).Trim();
            }
        }

        return parsed;
    }

    /// <summary>The slot and source of <paramref name="mid"/>, or null for the local uplink mids.</summary>
    public static RemoteMid? RemoteMidInfo(uint mid)
    {
        if (mid < RemoteMidBase)
        {
            return null;
        }

        var offset = mid - RemoteMidBase;
        var source = (offset % MidsPerSlot) switch
        {
            0 => TrackSource.Microphone,
            1 => TrackSource.Camera,
            _ => TrackSource.Screen,
        };
        return new RemoteMid(offset / MidsPerSlot, source);
    }

    /// <summary>The first (audio) mid of remote slot <paramref name="slot"/>.</summary>
    public static uint SlotFirstMid(uint slot) => RemoteMidBase + MidsPerSlot * slot;

    /// <summary>
    /// Reads the remote-slot structure of a server offer. The mode is fixed when any remote m-line uses a
    /// <c>slot-N</c> msid and dynamic when one uses <c>u&lt;user&gt;-p&lt;peer&gt;</c>.
    /// </summary>
    public static OfferLayout AnalyzeOffer(string offer)
    {
        var parsed = Parse(offer);
        var remote = new List<RemoteSection>();
        var mode = DownlinkMode.Unknown;
        foreach (var section in parsed.Sections)
        {
            if (section.Mid is null || !uint.TryParse(section.Mid, NumberStyles.None, CultureInfo.InvariantCulture, out var mid))
            {
                continue;
            }

            if (RemoteMidInfo(mid) is not { } info)
            {
                continue;
            }

            switch (section.Occupant)
            {
                case Occupant.Slot:
                    mode = DownlinkMode.Fixed;
                    break;
                case Occupant.Peer when mode == DownlinkMode.Unknown:
                    mode = DownlinkMode.Dynamic;
                    break;
            }

            remote.Add(new RemoteSection(mid, info.Slot, info.Source, section.Direction, section.Occupant));
        }

        return new OfferLayout(parsed, (uint)remote.Count / MidsPerSlot, remote, mode);
    }

    /// <summary>
    /// Checks an answer the way the server does (<c>validate_answer_mline_shape</c> in
    /// <c>mezon-sfu/src/protocol/signaling/signaling.c</c>): exactly 3 + 3×slots m-lines, audio/video/video per
    /// triple, and each m-line's <c>a=mid</c> equal to its index. Returns null when valid, else the problem.
    /// </summary>
    public static string? CheckAnswerShape(string answer, uint slots) =>
        CheckAnswerShape(answer, RemoteMidBase + MidsPerSlot * (long)slots, index => index % MidsPerSlot == 0);

    /// <summary>
    /// <see cref="CheckAnswerShape(string, uint)"/> for a decoupled room (<c>validate_answer_mline_shape_layout</c>):
    /// 3 + A + C + S m-lines, audio for mid 0 and the audio pool, video for the others.
    /// </summary>
    public static string? CheckAnswerShape(string answer, MediaPoolLayout pool) =>
        CheckAnswerShape(answer, pool.MLines, index => index == MidMicrophone || (index >= RemoteMidBase && index < RemoteMidBase + (long)pool.Audio));

    private static string? CheckAnswerShape(string answer, long expected, Func<long, bool> audio)
    {
        var sections = Parse(answer).Sections;
        if (sections.Count != expected)
        {
            return $"expected {expected} m-lines, found {sections.Count}";
        }

        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            var want = audio(index) ? "audio" : "video";
            if (section.Media != want)
            {
                return $"m-line {index} is {section.Media}, expected {want}";
            }

            var mids = section.Lines.Count(l => l.StartsWith("a=mid:", StringComparison.Ordinal));
            if (mids != 1)
            {
                return $"m-line {index} has {mids} a=mid lines";
            }

            if (section.Mid != index.ToString(CultureInfo.InvariantCulture))
            {
                return $"m-line {index} has mid {section.Mid}";
            }
        }

        return null;
    }

    /// <summary>The direction this client answers for an offered m-line.</summary>
    /// <param name="camera">Whether this client publishes a camera (speakers).</param>
    public static string AnswerDirection(uint mid, string? offered, bool camera)
    {
        if (offered == "inactive")
        {
            return "inactive";
        }

        return mid switch
        {
            // Mid 0 is always sendonly: the server's voice activity detection and push-to-talk use it.
            MidMicrophone => "sendonly",

            // The camera m-line is never renegotiated, so a speaker sends from the first answer.
            MidCamera => camera ? "sendonly" : "inactive",

            // Screen sharing is not published by this SDK.
            MidScreen => "inactive",
            _ => offered is "sendonly" or "sendrecv" ? "recvonly" : "inactive",
        };
    }

    /// <summary>
    /// Rewrites a WebRTC stack's answer so that it answers <paramref name="offer"/> the way the protocol needs: one
    /// section per offered m-line with the offered mid, in the offer's order; the directions from
    /// <see cref="AnswerDirection"/>; only payload types the offer listed; and a BUNDLE group of exactly the
    /// offered mids. Sections the stack did not produce are answered as rejected (port 0, inactive). Throws when
    /// the answer is not usable (no sections).
    /// </summary>
    public static string NormalizeAnswer(string offer, string answer, bool camera)
    {
        var offered = Parse(offer);
        var produced = Parse(answer);
        if (produced.Sections.Count == 0)
        {
            throw new FormatException("the answer has no media sections");
        }

        var sb = new StringBuilder();
        foreach (var line in produced.SessionLines)
        {
            if (line.StartsWith("a=group:BUNDLE", StringComparison.Ordinal))
            {
                continue;
            }

            sb.Append(line).Append("\r\n");
        }

        sb.Append("a=group:BUNDLE ").Append(string.Join(" ", offered.Sections.Select(s => s.Mid).Where(m => m is not null))).Append("\r\n");

        var template = produced.Sections.FirstOrDefault(s => s.Port != 0) ?? produced.Sections[0];
        foreach (var offerSection in offered.Sections)
        {
            var mid = offerSection.Mid ?? "";
            var midNumber = uint.TryParse(mid, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : uint.MaxValue;
            var direction = AnswerDirection(midNumber, offerSection.Direction, camera);
            var section = produced.Sections.FirstOrDefault(s => s.Mid == mid && s.Media == offerSection.Media);
            if (section is null)
            {
                AppendRejected(sb, offerSection, template, mid);
                continue;
            }

            var offeredPts = new HashSet<int>(offerSection.Codecs.Keys);
            var mLine = section.Lines[0].Split(' ');
            var kept = mLine.Skip(3).Where(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var pt) && offeredPts.Contains(pt)).ToList();
            if (kept.Count == 0)
            {
                AppendRejected(sb, offerSection, template, mid);
                continue;
            }

            sb.Append(string.Join(" ", mLine.Take(3).Concat(kept))).Append("\r\n");
            var keptPts = new HashSet<string>(kept);
            foreach (var line in section.Lines.Skip(1))
            {
                if (Directions.Contains(line.StartsWith("a=", StringComparison.Ordinal) ? line.Substring(2) : ""))
                {
                    continue;
                }

                if (PayloadAttribute(line) is { } pt && !keptPts.Contains(pt))
                {
                    continue;
                }

                sb.Append(line).Append("\r\n");
            }

            sb.Append("a=").Append(direction).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string? PayloadAttribute(string line)
    {
        foreach (var prefix in new[] { "a=rtpmap:", "a=fmtp:", "a=rtcp-fb:" })
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = line.Substring(prefix.Length);
                var end = rest.IndexOf(' ');
                return end > 0 ? rest.Substring(0, end) : rest;
            }
        }

        return null;
    }

    private static void AppendRejected(StringBuilder sb, SdpSection offerSection, SdpSection template, string mid)
    {
        var pt = offerSection.Codecs.Keys.Cast<int?>().FirstOrDefault() ?? (offerSection.Media == "audio" ? 111 : 96);
        var profile = template.Lines[0].Split(' ').ElementAtOrDefault(2) ?? "UDP/TLS/RTP/SAVPF";
        sb.Append("m=").Append(offerSection.Media).Append(" 0 ").Append(profile).Append(' ').Append(pt).Append("\r\n");
        foreach (var line in template.Lines.Skip(1))
        {
            if (line.StartsWith("c=", StringComparison.Ordinal) || line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal)
                || line.StartsWith("a=ice-pwd:", StringComparison.Ordinal) || line.StartsWith("a=fingerprint:", StringComparison.Ordinal)
                || line.StartsWith("a=setup:", StringComparison.Ordinal))
            {
                sb.Append(line).Append("\r\n");
            }
        }

        sb.Append("a=mid:").Append(mid).Append("\r\n");
        if (offerSection.Codecs.ContainsKey(pt))
        {
            sb.Append("a=rtpmap:").Append(pt).Append(' ').Append(offerSection.Lines.First(l => l.StartsWith($"a=rtpmap:{pt} ", StringComparison.Ordinal)).Substring($"a=rtpmap:{pt} ".Length)).Append("\r\n");
        }

        sb.Append("a=inactive\r\n");
    }
}
