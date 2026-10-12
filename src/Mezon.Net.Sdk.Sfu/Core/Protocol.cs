using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Mezon.Net.Sdk.Sfu.Core;

// Wire format of the mezon-sfu signaling protocol: JSON text frames on one WebSocket.
//
// Client messages are encoded exactly as the server expects them; server messages are decoded
// tolerantly (older builds send mids as strings, unknown types are not fatal) into normalized
// records where 64-bit ids stay decimal strings.

/// <summary>A video uplink of a publisher.</summary>
internal enum VideoSource
{
    Camera,
    Screen,
}

/// <summary>A message this client sends.</summary>
internal abstract record ClientMessage
{
    public abstract string TypeName { get; }

    /// <summary>Authenticates and joins the room named by the token's claims.</summary>
    public sealed record Join(Role Role, ScreenCodec ScreenCodec, string Token) : ClientMessage
    {
        public override string TypeName => "join";

        protected override bool PrintMembers(StringBuilder builder)
        {
            if (base.PrintMembers(builder))
            {
                builder.Append(", ");
            }

            builder.Append("Role = ").Append(Role).Append(", ScreenCodec = ").Append(ScreenCodec).Append(", Token = <redacted>");
            return true;
        }
    }

    /// <summary>Answers the server offer with generation <see cref="OfferGeneration"/>.</summary>
    public sealed record Answer(long OfferGeneration, string Sdp) : ClientMessage
    {
        public override string TypeName => "answer";
    }

    /// <summary>Sets the microphone state. A revision must increase strictly per connection.</summary>
    public sealed record Mute(bool IsMute, long? MuteRevision) : ClientMessage
    {
        public override string TypeName => "mute";
    }

    public sealed record Camera(bool Active) : ClientMessage
    {
        public override string TypeName => "camera";
    }

    public sealed record ShareScreen(bool Active) : ClientMessage
    {
        public override string TypeName => "share_screen";
    }

    public sealed record PushToTalk(bool Active) : ClientMessage
    {
        public override string TypeName => "push_to_talk";
    }

    /// <summary>Gates which remote video this client receives.</summary>
    public sealed record Visibility(bool? VisibleCamera, bool? VisibleScreen, bool? Visible) : ClientMessage
    {
        public override string TypeName => "visibility";
    }

    public sealed record RequestKeyframe(VideoSource Kind, uint PublisherId) : ClientMessage
    {
        public override string TypeName => "request_keyframe";
    }

    /// <summary>Sends a text message to the rest of the room (at most 2047 UTF-8 bytes).</summary>
    public sealed record SendMessage(string Message) : ClientMessage
    {
        public override string TypeName => "send_message";
    }

    /// <summary>Replaces this participant's metadata (at most 511 UTF-8 bytes).</summary>
    public sealed record PeerUpdate(string Metadata) : ClientMessage
    {
        public override string TypeName => "peer_update";
    }

    /// <summary>Applies a moderation token (a meet token for the target whose metadata is "mute" or "kick").</summary>
    public sealed record ParticipantAction(string Token) : ClientMessage
    {
        public override string TypeName => "participant_action";

        protected override bool PrintMembers(StringBuilder builder)
        {
            if (base.PrintMembers(builder))
            {
                builder.Append(", ");
            }

            builder.Append("Token = <redacted>");
            return true;
        }
    }

    public sealed record Ping : ClientMessage
    {
        public override string TypeName => "ping";
    }

    public sealed record Pong : ClientMessage
    {
        public override string TypeName => "pong";
    }
}

/// <summary>A STUN/TURN server announced in <c>joined</c>.</summary>
internal sealed record WireIceServer(IReadOnlyList<string> Urls, string? Username, string? Credential)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Urls = ").Append(Urls).Append(", Username = ").Append(Username).Append(", Credential = ").Append(Credential is null ? null : "<redacted>");
        return true;
    }
}

/// <summary>
/// Where a peer's media lands in the recipient's PeerConnection. Mids are 0 and <see cref="Slot"/> is null when
/// unmapped (the server's sentinels are mid 0 and slot 0xFFFFFFFF).
/// </summary>
internal readonly record struct SlotMapping(uint MidAudio, uint MidVideo, uint MidScreen, uint? Slot, long AssignmentGeneration)
{
    public bool IsMapped => Slot.HasValue || MidAudio > 0 || MidVideo > 0 || MidScreen > 0;
}

/// <summary>One entry of <c>room_snapshot</c> or <c>peer_joined</c>.</summary>
internal sealed record Member(ParticipantState State, SlotMapping Mapping, string Ufrag);

internal abstract record ServerMessage
{
    public abstract string TypeName { get; }

    public sealed record Joined(string Room, uint PeerId, string Metadata, IReadOnlyList<WireIceServer> IceServers) : ServerMessage
    {
        public override string TypeName => "joined";
    }

    public sealed record Offer(long OfferGeneration, string Sdp) : ServerMessage
    {
        public override string TypeName => "offer";
    }

    public sealed record RoomSnapshot(string Room, long RoomRevision, uint SelfPeerId, int ParticipantCount, IReadOnlyList<Member> Members) : ServerMessage
    {
        public override string TypeName => "room_snapshot";
    }

    public sealed record PeerJoined(long RoomRevision, int ParticipantCount, Member Peer) : ServerMessage
    {
        public override string TypeName => "peer_joined";
    }

    public sealed record PeerLeft(long RoomRevision, int ParticipantCount, string Ufrag, string UserId, uint PeerId, SlotMapping Mapping) : ServerMessage
    {
        public override string TypeName => "peer_left";
    }

    public sealed record PeerUpdated(ParticipantState State) : ServerMessage
    {
        public override string TypeName => "peer_updated";
    }

    public sealed record SlotAssigned(
        uint Slot,
        long Generation,
        uint PeerId,
        string UserId,
        uint MidAudio,
        uint MidVideo,
        uint MidScreen,
        bool AudioActive,
        bool VideoActive,
        bool ScreenActive) : ServerMessage
    {
        public override string TypeName => "slot_assigned";
    }

    /// <summary>Retires <see cref="Generation"/> of <see cref="Slot"/>.</summary>
    public sealed record SlotReleased(uint Slot, long Generation) : ServerMessage
    {
        public override string TypeName => "slot_released";
    }

    public sealed record MuteChanged(bool IsMute, long? MuteRevision) : ServerMessage
    {
        public override string TypeName => "mute_changed";
    }

    public sealed record PushToTalkChanged(bool Active) : ServerMessage
    {
        public override string TypeName => "push_to_talk_changed";
    }

    public sealed record VisibilityChanged(bool Visible, bool Camera, bool Screen) : ServerMessage
    {
        public override string TypeName => "visibility_changed";
    }

    public sealed record KeyframeRequested(bool Success, bool Cached, string Kind, uint? PublisherId) : ServerMessage
    {
        public override string TypeName => "keyframe_requested";
    }

    public sealed record ParticipantActionCompleted(string Action, string UserId, int Affected) : ServerMessage
    {
        public override string TypeName => "participant_action_completed";
    }

    public sealed record RoomMessage(string Message, string UserId, uint PeerId) : ServerMessage
    {
        public override string TypeName => "room_message";
    }

    public sealed record MessageSent : ServerMessage
    {
        public override string TypeName => "message_sent";
    }

    /// <summary>A server error; <see cref="Code"/> is a machine-readable code such as "invalid_token".</summary>
    public sealed record Error(string Code) : ServerMessage
    {
        public override string TypeName => "error";
    }

    public sealed record Ping : ServerMessage
    {
        public override string TypeName => "ping";
    }

    public sealed record Pong : ServerMessage
    {
        public override string TypeName => "pong";
    }

    /// <summary>A well-formed frame whose type this SDK does not know. Never fatal.</summary>
    public sealed record Unknown(string RawType, string Raw) : ServerMessage
    {
        public override string TypeName => "unknown";
    }
}

/// <summary>A client message that cannot be sent.</summary>
internal sealed class EncodeException : Exception
{
    public EncodeException(string field, string reason)
        : base($"protocol: {field} {reason}")
    {
        Field = field;
    }

    public string Field { get; }
}

/// <summary>A server frame that cannot be used.</summary>
internal sealed class DecodeException : Exception
{
    public DecodeException(string code, string detail)
        : base($"protocol: {code}: {detail}")
    {
        Code = code;
    }

    public string Code { get; }
}

internal static class Protocol
{
    /// <summary>Largest accepted free-text values, in UTF-8 bytes (the server's fixed buffers, minus NUL).</summary>
    public const int MaxTokenBytes = 4095;

    public const int MaxMetadataBytes = 511;
    public const int MaxMessageBytes = 2047;
    public const int MaxSdpBytes = 1_048_575;

    private const long MaxSafeInteger = (1L << 53) - 1;
    private const long SlotUnassigned = 0xFFFF_FFFF;

    /// <summary>
    /// Makes free text safe for the server's JSON reader, which unescapes only <c>\n \r \t \" \\ \/</c> and keeps
    /// the letter of any other escape: drops C0 control characters other than tab, LF and CR, and replaces lone
    /// UTF-16 surrogates with U+FFFD (what UTF-8 encoding would write anyway).
    /// </summary>
    public static string SanitizeText(string text)
    {
        StringBuilder? sb = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            char? replacement = null;
            var drop = false;
            if (c < ' ' && c != '\t' && c != '\n' && c != '\r')
            {
                drop = true;
            }
            else if (char.IsHighSurrogate(c))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    sb?.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }

                replacement = '�';
            }
            else if (char.IsLowSurrogate(c))
            {
                replacement = '�';
            }

            if (drop || replacement.HasValue)
            {
                sb ??= new StringBuilder(text, 0, i, text.Length);
                if (replacement.HasValue)
                {
                    sb.Append(replacement.Value);
                }

                continue;
            }

            sb?.Append(c);
        }

        return sb?.ToString() ?? text;
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }

    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();

        public Writer(string type)
        {
            _sb.Append("{\"type\":");
            WriteString(_sb, type);
        }

        private void Key(string name) => _sb.Append(",\"").Append(name).Append("\":");

        public void Boolean(string name, bool value)
        {
            Key(name);
            _sb.Append(value ? "true" : "false");
        }

        public void Integer(string name, long value, long min, long max)
        {
            if (value < min || value > max)
            {
                throw new EncodeException(name, $"must be in [{min}, {max}]");
            }

            Key(name);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Text(string name, string value, int limit)
        {
            var clean = SanitizeText(value);
            var bytes = Encoding.UTF8.GetByteCount(clean);
            if (bytes > limit)
            {
                throw new EncodeException(name, $"is {bytes} bytes; the server accepts at most {limit}");
            }

            Key(name);
            WriteString(_sb, clean);
        }

        public void Enumeration(string name, string value)
        {
            Key(name);
            WriteString(_sb, value);
        }

        public string Finish() => _sb.Append('}').ToString();
    }

    /// <summary>
    /// Serializes a client message for the wire. The server finds each field by the first occurrence of
    /// <c>"name"</c> anywhere in the frame, so keys are written in a fixed order: <c>type</c> first, scalar fields
    /// next, the single free-text field (<c>token</c>, <c>sdp</c>, <c>message</c> or <c>metadata</c>) last. No
    /// generic serializer is used because the order of keys matters and only the escapes the server understands
    /// may appear (System.Text.Json escapes <c>&lt;&gt;&amp;</c> and non-ASCII as <c>\uXXXX</c>).
    /// </summary>
    public static string Encode(ClientMessage message)
    {
        var w = new Writer(message.TypeName);
        switch (message)
        {
            case ClientMessage.Join join:
                w.Enumeration("role", join.Role == Role.Audience ? "audience" : "speaker");
                w.Enumeration("screen_codec", join.ScreenCodec == ScreenCodec.Vp9 ? "vp9" : "vp8");
                w.Text("token", join.Token, MaxTokenBytes);
                break;
            case ClientMessage.Answer answer:
                w.Integer("offer_generation", answer.OfferGeneration, 0, MaxSafeInteger);
                w.Text("sdp", answer.Sdp, MaxSdpBytes);
                break;
            case ClientMessage.Mute mute:
                w.Boolean("is_mute", mute.IsMute);
                if (mute.MuteRevision is { } revision)
                {
                    w.Integer("mute_revision", revision, 1, MaxSafeInteger);
                }

                break;
            case ClientMessage.Camera camera:
                w.Boolean("active", camera.Active);
                break;
            case ClientMessage.ShareScreen screen:
                w.Boolean("active", screen.Active);
                break;
            case ClientMessage.PushToTalk ptt:
                w.Boolean("active", ptt.Active);
                break;
            case ClientMessage.Visibility visibility:
                if (visibility.VisibleCamera is null && visibility.VisibleScreen is null && visibility.Visible is null)
                {
                    throw new EncodeException("visibility", "needs at least one field");
                }

                if (visibility.VisibleCamera is { } cameraVisible)
                {
                    w.Boolean("visible_camera", cameraVisible);
                }

                if (visibility.VisibleScreen is { } screenVisible)
                {
                    w.Boolean("visible_screen", screenVisible);
                }

                if (visibility.Visible is { } visible)
                {
                    w.Boolean("visible", visible);
                }

                break;
            case ClientMessage.RequestKeyframe keyframe:
                w.Enumeration("kind", keyframe.Kind == VideoSource.Screen ? "screen" : "camera");
                w.Integer("publisher_id", keyframe.PublisherId, 1, uint.MaxValue);
                break;
            case ClientMessage.SendMessage send:
                if (SanitizeText(send.Message).Length == 0)
                {
                    throw new EncodeException("message", "must not be empty");
                }

                w.Text("message", send.Message, MaxMessageBytes);
                break;
            case ClientMessage.PeerUpdate update:
                w.Text("metadata", update.Metadata, MaxMetadataBytes);
                break;
            case ClientMessage.ParticipantAction action:
                w.Text("token", action.Token, MaxTokenBytes);
                break;
        }

        return w.Finish();
    }

    // -----------------------------------------------------------------------------------------
    // Decoding

    private readonly struct Fields
    {
        private readonly JsonElement _o;

        public Fields(JsonElement o) => _o = o;

        private bool TryGet(string key, out JsonElement value)
        {
            if (_o.ValueKind == JsonValueKind.Object && _o.TryGetProperty(key, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        public bool Has(string key) => TryGet(key, out var v) && v.ValueKind != JsonValueKind.Null;

        public void Required(string key, string type)
        {
            if (!Has(key))
            {
                throw new DecodeException("missing_field", $"{type} is missing \"{key}\"");
            }
        }

        public JsonElement Child(string key) => TryGet(key, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

        public string Str(string key) => TryGet(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        public bool Boolean(string key)
        {
            if (!TryGet(key, out var v))
            {
                return false;
            }

            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => v.GetString() == "true",
                _ => false,
            };
        }

        private static DecodeException Overflow(string key, string text) => new("integer_overflow", $"{key} = {text}");

        /// <summary>A JSON number (or a numeric string from older builds).</summary>
        public long Int(string key, long fallback)
        {
            if (!TryGet(key, out var v))
            {
                return fallback;
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.Number:
                    if (v.TryGetInt64(out var i))
                    {
                        return i;
                    }

                    if (v.TryGetDouble(out var d) && Math.Floor(d) == d && Math.Abs(d) < 9.2e18)
                    {
                        return (long)d;
                    }

                    throw Overflow(key, v.GetRawText());
                case JsonValueKind.String:
                    var t = (v.GetString() ?? "").Trim();
                    if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }

                    var digits = t.StartsWith("-", StringComparison.Ordinal) ? t.Substring(1) : t;
                    if (digits.Length > 0 && digits.All(c => c >= '0' && c <= '9'))
                    {
                        throw Overflow(key, t);
                    }

                    return fallback;
                default:
                    return fallback;
            }
        }

        public uint PeerId(string key)
        {
            var n = Int(key, 0);
            return n is >= 0 and <= uint.MaxValue ? (uint)n : 0;
        }

        public uint Mid(string key)
        {
            var n = Int(key, 0);
            return n is > 0 and <= uint.MaxValue ? (uint)n : 0;
        }

        /// <summary>64-bit ids stay decimal strings; numbers are accepted when they are integers.</summary>
        public string UserId(string key)
        {
            if (!TryGet(key, out var v))
            {
                return "0";
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.String:
                    var s = v.GetString() ?? "";
                    return long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
                           || ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                        ? s
                        : "0";
                case JsonValueKind.Number:
                    if (v.TryGetInt64(out var i))
                    {
                        return i.ToString(CultureInfo.InvariantCulture);
                    }

                    if (v.TryGetUInt64(out var u))
                    {
                        return u.ToString(CultureInfo.InvariantCulture);
                    }

                    throw Overflow(key, v.GetRawText());
                default:
                    return "0";
            }
        }

        public string RoomId(string key)
        {
            if (!TryGet(key, out var v))
            {
                return "";
            }

            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.GetRawText(),
                _ => "",
            };
        }

        public SlotMapping SlotMapping()
        {
            var key = Has("slot") ? "slot" : "remote_slot";
            var slot = Int(key, SlotUnassigned);
            return new SlotMapping(
                Mid("mid_audio"),
                Mid("mid_video"),
                Mid("mid_screen"),
                slot is >= 0 and < SlotUnassigned ? (uint)slot : null,
                Int("assignment_generation", 0));
        }

        public ParticipantState PeerState() => new(
            PeerId("peer_id"),
            UserId("user_id"),
            Str("metadata"),
            Str("role") == "audience" ? Role.Audience : Role.Speaker,
            Boolean("is_mute"),
            Boolean("camera_requested"),
            Boolean("camera_active"),
            Boolean("screen_requested"),
            Boolean("screen_active"));
    }

    private static Member ReadMember(JsonElement value)
    {
        var f = new Fields(value);
        return new Member(f.PeerState(), f.SlotMapping(), f.Str("ufrag"));
    }

    private static IReadOnlyList<WireIceServer> ReadIceServers(JsonElement root)
    {
        var list = new List<WireIceServer>();
        if (!root.TryGetProperty("iceServers", out var servers) || servers.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var entry in servers.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("urls", out var urls))
            {
                continue;
            }

            var parsed = urls.ValueKind switch
            {
                JsonValueKind.String => new List<string> { urls.GetString() ?? "" },
                JsonValueKind.Array => urls.EnumerateArray().Where(u => u.ValueKind == JsonValueKind.String).Select(u => u.GetString() ?? "").ToList(),
                _ => new List<string>(),
            };
            if (parsed.Count == 0)
            {
                continue;
            }

            string? Optional(string key) => entry.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            list.Add(new WireIceServer(parsed, Optional("username"), Optional("credential")));
        }

        return list;
    }

    private static int Count(long value) => (int)Math.Min(Math.Max(value, 0), int.MaxValue);

    private static JsonDocument Parse(string frame)
    {
        try
        {
            return JsonDocument.Parse(frame);
        }
        catch (JsonException e)
        {
            if (frame.Any(c => c < ' '))
            {
                // The server relays participant text without escaping control characters
                // (mezonai/mezon-sfu#480): drop them and parse again.
                var cleaned = new string(frame.Where(c => c >= ' ').ToArray());
                try
                {
                    return JsonDocument.Parse(cleaned);
                }
                catch (JsonException retry)
                {
                    throw new DecodeException("invalid_json", retry.Message);
                }
            }

            throw new DecodeException("invalid_json", e.Message);
        }
    }

    /// <summary>
    /// Parses one server text frame. Decoding is tolerant: absent optional fields take neutral defaults, and
    /// well-formed frames with an unknown type decode to <see cref="ServerMessage.Unknown"/>. It fails for invalid
    /// JSON, a frame without a string type, a missing field the message cannot be used without, or an integer
    /// beyond 64 bits. Raw control characters, which the server relays unescaped from participant text, are removed.
    /// </summary>
    public static ServerMessage Decode(string frame)
    {
        using var doc = Parse(frame);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new DecodeException("missing_type", "frame has no string \"type\"");
        }

        var type = typeElement.GetString() ?? "";
        var f = new Fields(root);
        switch (type)
        {
            case "joined":
                return new ServerMessage.Joined(f.RoomId("room"), f.PeerId("peer_id"), f.Str("metadata"), ReadIceServers(root));
            case "offer":
            {
                f.Required("sdp", "offer");
                f.Required("offer_generation", "offer");
                var sdp = f.Str("sdp");
                if (sdp.Length == 0)
                {
                    throw new DecodeException("missing_field", "offer has an empty sdp");
                }

                return new ServerMessage.Offer(f.Int("offer_generation", -1), sdp);
            }

            case "room_snapshot":
            {
                var members = new List<Member>();
                if (root.TryGetProperty("members", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in list.EnumerateArray())
                    {
                        members.Add(ReadMember(m));
                    }
                }

                return new ServerMessage.RoomSnapshot(
                    f.RoomId("room"),
                    f.Int("room_revision", 0),
                    f.PeerId("self_peer_id"),
                    Count(f.Int("participant_count", 0)),
                    members);
            }

            case "peer_joined":
                f.Required("peer", "peer_joined");
                return new ServerMessage.PeerJoined(f.Int("room_revision", 0), Count(f.Int("participant_count", 0)), ReadMember(f.Child("peer")));
            case "peer_left":
                f.Required("peer_id", "peer_left");
                return new ServerMessage.PeerLeft(
                    f.Int("room_revision", 0),
                    Count(f.Int("participant_count", 0)),
                    f.Str("ufrag"),
                    f.UserId("user_id"),
                    f.PeerId("peer_id"),
                    f.SlotMapping());
            case "peer_updated":
                f.Required("peer", "peer_updated");
                return new ServerMessage.PeerUpdated(new Fields(f.Child("peer")).PeerState());
            case "slot_assigned":
            {
                f.Required("slot", "slot_assigned");
                f.Required("generation", "slot_assigned");
                var slot = (uint)Math.Min(Math.Max(f.Int("slot", 0), 0), uint.MaxValue / 3 - 2);
                long first = 3 + 3L * slot;
                var fields = f;
                uint MidOr(string key, long fallback) => (uint)Math.Min(Math.Max(fields.Int(key, fallback), 0), uint.MaxValue);
                return new ServerMessage.SlotAssigned(
                    slot,
                    f.Int("generation", 0),
                    f.PeerId("peer_id"),
                    f.UserId("user_id"),
                    MidOr("mid_audio", first),
                    MidOr("mid_video", first + 1),
                    MidOr("mid_screen", first + 2),
                    f.Boolean("audio_active"),
                    f.Boolean("video_active"),
                    f.Boolean("screen_active"));
            }

            case "slot_released":
                f.Required("slot", "slot_released");
                f.Required("generation", "slot_released");
                return new ServerMessage.SlotReleased((uint)Math.Min(Math.Max(f.Int("slot", 0), 0), uint.MaxValue), f.Int("generation", 0));
            case "mute_changed":
            {
                var revision = f.Int("mute_revision", -1);
                return new ServerMessage.MuteChanged(f.Boolean("is_mute"), revision >= 0 && f.Has("mute_revision") ? revision : null);
            }

            case "push_to_talk_changed":
                return new ServerMessage.PushToTalkChanged(f.Boolean("active"));
            case "visibility_changed":
                return new ServerMessage.VisibilityChanged(f.Boolean("visible"), f.Boolean("camera"), f.Boolean("screen"));
            case "keyframe_requested":
            {
                var publisher = f.PeerId("publisher_id");
                return new ServerMessage.KeyframeRequested(f.Boolean("success"), f.Boolean("cached"), f.Str("kind"), publisher > 0 ? publisher : null);
            }

            case "participant_action_completed":
                return new ServerMessage.ParticipantActionCompleted(f.Str("action"), f.UserId("user_id"), Count(f.Int("affected", 0)));
            case "room_message":
                return new ServerMessage.RoomMessage(f.Str("message"), f.UserId("user_id"), f.PeerId("peer_id"));
            case "message_sent":
                return new ServerMessage.MessageSent();
            case "error":
                return new ServerMessage.Error(root.TryGetProperty("message", out var code) && code.ValueKind == JsonValueKind.String
                    ? code.GetString() ?? "unknown_error"
                    : "unknown_error");
            case "ping":
                return new ServerMessage.Ping();
            case "pong":
                return new ServerMessage.Pong();
            default:
                return new ServerMessage.Unknown(type, root.GetRawText());
        }
    }
}
