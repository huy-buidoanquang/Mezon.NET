using System.Text;
using System.Text.Json;
using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu.Tests;

/// <summary>Skipped unless mezon-sfu/docs/protocol (mezonai/mezon-sfu#478) is next to this repository or in MEZON_SFU_PROTOCOL_DIR.</summary>
public sealed class ProtocolSpecFactAttribute : FactAttribute
{
    public static string? Dir => TestPaths.Optional("MEZON_SFU_PROTOCOL_DIR", Path.Combine("mezon-sfu", "docs", "protocol"), "close-codes.json");

    public ProtocolSpecFactAttribute()
    {
        if (Dir is null)
        {
            Skip = "mezon-sfu/docs/protocol not found (set MEZON_SFU_PROTOCOL_DIR)";
        }
    }
}

/// <summary>Skipped unless the canonical mezon-protocol checkout is next to this repository or in MEZON_PROTOCOL_DIR.</summary>
public sealed class CanonicalProtoFactAttribute : FactAttribute
{
    public static string? Dir => TestPaths.Optional("MEZON_PROTOCOL_DIR", "mezon-protocol", Path.Combine("api", "api.proto"));

    public CanonicalProtoFactAttribute()
    {
        if (Dir is null)
        {
            Skip = "mezon-protocol not found next to Mezon.Net (set MEZON_PROTOCOL_DIR)";
        }
    }
}

public class ProtocolTests
{
    private static string Enc(ClientMessage message) => Protocol.Encode(message);

    private static void Fails(ClientMessage message, string field)
    {
        var e = Assert.Throws<EncodeException>(() => Protocol.Encode(message));
        Assert.Equal(field, e.Field);
    }

    [Fact]
    public void Encode_orders_keys_type_first_and_free_text_last()
    {
        Assert.Equal(
            """{"type":"join","role":"audience","screen_codec":"vp9","token":"a.b.c"}""",
            Enc(new ClientMessage.Join(Role.Audience, ScreenCodec.Vp9, "a.b.c")));
        Assert.Equal("""{"type":"answer","offer_generation":3,"sdp":"v=0"}""", Enc(new ClientMessage.Answer(3, "v=0")));
    }

    [Fact]
    public void Encode_scalars_and_their_ranges()
    {
        var cases = new (ClientMessage Message, string Want)[]
        {
            (new ClientMessage.Mute(true, null), """{"type":"mute","is_mute":true}"""),
            (new ClientMessage.Mute(false, 7), """{"type":"mute","is_mute":false,"mute_revision":7}"""),
            (new ClientMessage.Camera(true), """{"type":"camera","active":true}"""),
            (new ClientMessage.ShareScreen(false), """{"type":"share_screen","active":false}"""),
            (new ClientMessage.PushToTalk(true), """{"type":"push_to_talk","active":true}"""),
            (new ClientMessage.RequestKeyframe(VideoSource.Screen, 31), """{"type":"request_keyframe","kind":"screen","publisher_id":31}"""),
            (new ClientMessage.Visibility(null, false, null), """{"type":"visibility","visible_screen":false}"""),
            (new ClientMessage.Ping(), """{"type":"ping"}"""),
            (new ClientMessage.Pong(), """{"type":"pong"}"""),
        };
        foreach (var (message, want) in cases)
        {
            Assert.Equal(want, Enc(message));
        }

        Fails(new ClientMessage.Visibility(null, null, null), "visibility");
        Fails(new ClientMessage.Mute(false, 0), "mute_revision");
        Fails(new ClientMessage.Mute(false, 1L << 53), "mute_revision");
        Fails(new ClientMessage.RequestKeyframe(VideoSource.Camera, 0), "publisher_id");
        Fails(new ClientMessage.Answer(-1, "v=0"), "offer_generation");
    }

    [Fact]
    public void Encode_free_text_with_only_the_escapes_the_server_reads()
    {
        Fails(new ClientMessage.SendMessage(""), "message");
        Fails(new ClientMessage.SendMessage("\u0001\u0002"), "message");
        Enc(new ClientMessage.SendMessage(new string('x', Protocol.MaxMessageBytes)));
        Fails(new ClientMessage.SendMessage(new string('x', Protocol.MaxMessageBytes + 1)), "message");
        Fails(new ClientMessage.SendMessage(string.Concat(Enumerable.Repeat("é", 1024))), "message");
        Enc(new ClientMessage.PeerUpdate(new string('m', Protocol.MaxMetadataBytes)));
        Fails(new ClientMessage.PeerUpdate(new string('m', Protocol.MaxMetadataBytes + 1)), "metadata");

        var got = Enc(new ClientMessage.SendMessage("a\"b\\c\nd\te<&>\u2028\u0001😀"));
        Assert.Equal("{\"type\":\"send_message\",\"message\":\"a\\\"b\\\\c\\nd\\te<&>\u2028😀\"}", got);
        Assert.DoesNotContain("\\u", got);
        using var back = JsonDocument.Parse(got);
        Assert.Equal("a\"b\\c\nd\te<&>\u2028😀", back.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Sanitize_replaces_lone_surrogates_and_keeps_pairs()
    {
        Assert.Equal("a�b😀�", Protocol.SanitizeText("a\uD800b😀\uDC00"));
        Assert.Equal("tab\tlf\ncr\r", Protocol.SanitizeText("tab\tlf\ncr\r"));
        var text = "plain";
        Assert.Same(text, Protocol.SanitizeText(text));
    }

    [Fact]
    public void Decode_joined_and_offer()
    {
        var joined = Assert.IsType<ServerMessage.Joined>(Protocol.Decode(
            """{"type":"joined","room":"7","peer_id":12,"metadata":"","iceServers":[{"urls":"stun:1.2.3.4:3478"},{"urls":["turn:1.2.3.4:3478?transport=udp"],"username":"u","credential":"p"},{"urls":5}]}"""));
        Assert.Equal(("7", 12u, 2), (joined.Room, joined.PeerId, joined.IceServers.Count));
        Assert.Equal(new[] { "stun:1.2.3.4:3478" }, joined.IceServers[0].Urls);
        Assert.Null(joined.IceServers[0].Username);
        Assert.Equal(("u", "p"), (joined.IceServers[1].Username, joined.IceServers[1].Credential));

        var offer = Assert.IsType<ServerMessage.Offer>(Protocol.Decode("""{"type":"offer","offer_generation":9,"sdp":"v=0"}"""));
        Assert.Equal((9L, "v=0"), (offer.OfferGeneration, offer.Sdp));
        foreach (var bad in new[] { """{"type":"offer","offer_generation":1}""", """{"type":"offer","sdp":"v=0"}""", """{"type":"offer","offer_generation":1,"sdp":""}""" })
        {
            Assert.Throws<DecodeException>(() => Protocol.Decode(bad));
        }
    }

    private const string MemberJson =
        """{"peer_id":3,"user_id":"1840651530236071936","metadata":"Alice;https://cdn/a.png","role":"speaker","is_mute":false,"camera_requested":true,"camera_active":true,"screen_requested":false,"screen_active":false,"ufrag":"ab","mid_audio":3,"mid_video":4,"mid_screen":5,"slot":0,"assignment_generation":1}""";

    [Fact]
    public void Decode_snapshot_and_mappings()
    {
        var audience = MemberJson
            .Replace("\"peer_id\":3", "\"peer_id\":4")
            .Replace("\"role\":\"speaker\"", "\"role\":\"audience\"")
            .Replace("\"mid_audio\":3", "\"mid_audio\":0")
            .Replace("\"slot\":0", "\"slot\":4294967295");
        var snapshot = Assert.IsType<ServerMessage.RoomSnapshot>(Protocol.Decode(
            $$"""{"type":"room_snapshot","room":"1","room_revision":4,"self_peer_id":3,"participant_count":2,"members":[{{MemberJson}},{{audience}}]}"""));
        var first = snapshot.Members[0];
        Assert.Equal("1840651530236071936", first.State.UserId);
        Assert.True(first.State.CameraActive && first.State.Role == Role.Speaker);
        Assert.Equal(new SlotMapping(3, 4, 5, 0, 1), first.Mapping);
        var second = snapshot.Members[1];
        Assert.Equal((Role.Audience, 0u, (uint?)null), (second.State.Role, second.Mapping.MidAudio, second.Mapping.Slot));

        var left = Assert.IsType<ServerMessage.PeerLeft>(Protocol.Decode(
            """{"type":"peer_left","user_id":"9","peer_id":4,"mid_audio":"6","mid_video":"7","mid_screen":"8","remote_slot":1}"""));
        Assert.Equal((4u, 6u, 8u, (uint?)1), (left.PeerId, left.Mapping.MidAudio, left.Mapping.MidScreen, left.Mapping.Slot));
        Assert.Equal(
            new ServerMessage.SlotAssigned(2, 5, 8, "77", 9, 10, 11, false, false, false),
            Protocol.Decode("""{"type":"slot_assigned","slot":2,"generation":5,"peer_id":8,"user_id":"77"}"""));
        Assert.Equal(new ServerMessage.SlotReleased(2, 5), Protocol.Decode("""{"type":"slot_released","slot":2,"generation":5}"""));
        var updated = Assert.IsType<ServerMessage.PeerUpdated>(Protocol.Decode(
            """{"type":"peer_updated","peer":{"peer_id":4,"user_id":"9","role":"speaker","is_mute":"true"}}"""));
        Assert.True(updated.State.IsMuted && updated.State.PeerId == 4);
    }

    [Fact]
    public void Decode_drops_raw_control_characters_instead_of_the_frame()
    {
        var updated = Assert.IsType<ServerMessage.PeerUpdated>(Protocol.Decode(
            "{\"type\":\"peer_updated\",\"peer\":{\"peer_id\":5,\"user_id\":\"9\",\"metadata\":\"Ev\u0001il\u001f;\",\"role\":\"speaker\"}}"));
        Assert.Equal((5u, "Evil;"), (updated.State.PeerId, updated.State.Metadata));
        var snapshot = Assert.IsType<ServerMessage.RoomSnapshot>(Protocol.Decode(
            "{\"type\":\"room_snapshot\",\"room\":\"42\",\"room_revision\":1,\"self_peer_id\":1,\"participant_count\":1,\"members\":[{\"peer_id\":5,\"user_id\":\"9\",\"metadata\":\"\u0007\",\"role\":\"speaker\"}]}"));
        Assert.Single(snapshot.Members);
        var message = Assert.IsType<ServerMessage.RoomMessage>(Protocol.Decode("{\"type\":\"room_message\",\"message\":\"a\u0002b\",\"user_id\":\"5\",\"peer_id\":2}"));
        Assert.Equal("ab", message.Message);
        Assert.Throws<DecodeException>(() => Protocol.Decode("{\"type\":\"ping\"\u0001"));
    }

    [Fact]
    public void Decode_acknowledgements_errors_and_unknown_types()
    {
        var cases = new (string Frame, ServerMessage Want)[]
        {
            ("""{"type":"mute_changed","is_mute":true}""", new ServerMessage.MuteChanged(true, null)),
            ("""{"type":"mute_changed","is_mute":false,"mute_revision":3}""", new ServerMessage.MuteChanged(false, 3)),
            ("""{"type":"error","message":"invalid_token"}""", new ServerMessage.Error("invalid_token")),
            ("""{"type":"error"}""", new ServerMessage.Error("unknown_error")),
            ("""{"type":"message_sent"}""", new ServerMessage.MessageSent()),
            ("""{"type":"keyframe_requested","success":true,"cached":false,"kind":"screen"}""", new ServerMessage.KeyframeRequested(true, false, "screen", null)),
            ("""{"type":"room_message","message":"hi","user_id":"5","peer_id":2}""", new ServerMessage.RoomMessage("hi", "5", 2)),
            ("""{"type":"push_to_talk_changed","active":true}""", new ServerMessage.PushToTalkChanged(true)),
            ("""{"type":"visibility_changed","visible":true,"camera":false,"screen":true}""", new ServerMessage.VisibilityChanged(true, false, true)),
            ("""{"type":"participant_action_completed","action":"kick","user_id":"7","affected":2}""", new ServerMessage.ParticipantActionCompleted("kick", "7", 2)),
            ("""{"type":"ping"}""", new ServerMessage.Ping()),
        };
        foreach (var (frame, want) in cases)
        {
            Assert.Equal(want, Protocol.Decode(frame));
        }

        var unknown = Assert.IsType<ServerMessage.Unknown>(Protocol.Decode("""{"type":"role_changed","role":"speaker"}"""));
        Assert.Equal("role_changed", unknown.RawType);
        Assert.Contains("\"role\":\"speaker\"", unknown.Raw);
        foreach (var bad in new[] { "not json", "[]", """{"message":"x"}""", """{"type":5}""" })
        {
            Assert.Throws<DecodeException>(() => Protocol.Decode(bad));
        }

        Assert.Equal("integer_overflow", Assert.Throws<DecodeException>(() => Protocol.Decode("""{"type":"offer","offer_generation":92233720368547758070,"sdp":"v=0"}""")).Code);
        Assert.Throws<DecodeException>(() => Protocol.Decode("""{"type":"room_message","message":"x","user_id":184065153023607193600,"peer_id":1}"""));
        var big = Assert.IsType<ServerMessage.RoomMessage>(Protocol.Decode("""{"type":"room_message","message":"x","user_id":1840651530236071936,"peer_id":1}"""));
        Assert.Equal("1840651530236071936", big.UserId);
        var numericMid = Assert.IsType<ServerMessage.PeerLeft>(Protocol.Decode("""{"type":"peer_left","peer_id":"4","mid_audio":6.0,"slot":"2"}"""));
        Assert.Equal((4u, 6u, (uint?)2), (numericMid.PeerId, numericMid.Mapping.MidAudio, numericMid.Mapping.Slot));
    }

    [Fact]
    public void Close_and_error_tables()
    {
        foreach (var (code, action) in new[]
                 {
                     (1000, CloseAction.Terminal), (1001, CloseAction.Reconnect), (1002, CloseAction.RetryOnce), (1006, CloseAction.Reconnect),
                     (4003, CloseAction.Fatal), (4005, CloseAction.RefreshToken), (4006, CloseAction.Terminal), (4012, CloseAction.Terminal),
                     (4999, CloseAction.Reconnect),
                 })
        {
            Assert.Equal(action, CloseCodes.ActionFor(code));
        }

        Assert.Equal("DUPLICATE_SESSION", CloseCodes.NameOf(4012));
        Assert.Null(CloseCodes.NameOf(4999));
        Assert.Equal(new[] { "mute" }, ServerErrors.Find("stale_mute_revision")!.Sources);
        Assert.Null(ServerErrors.Find("nope"));
    }

    [ProtocolSpecFact]
    public void Tables_match_the_protocol_spec()
    {
        var dir = ProtocolSpecFactAttribute.Dir!;
        using var closes = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "close-codes.json")));
        var codes = closes.RootElement.GetProperty("codes").EnumerateArray().ToList();
        Assert.Equal(CloseCodes.All.Count, codes.Count);
        foreach (var c in codes)
        {
            var code = c.GetProperty("code").GetInt32();
            Assert.Equal(c.GetProperty("name").GetString(), CloseCodes.NameOf(code));
            Assert.Equal(c.GetProperty("action").GetString(), CloseCodes.ActionFor(code).ToSpecString());
        }

        using var errors = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "error-codes.json")));
        var list = errors.RootElement.GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal(ServerErrors.All.Count, list.Count);
        foreach (var e in list)
        {
            var code = e.GetProperty("code").GetString()!;
            var info = ServerErrors.Find(code);
            Assert.True(info is not null, $"{code} unknown to the SDK");
            Assert.Equal(e.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToArray(), info!.Sources);
            Assert.Equal((e.GetProperty("retryable").GetBoolean(), e.GetProperty("fatal").GetBoolean()), (info.Retryable, info.Fatal));
        }
    }

    internal static string Token(string payload)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("""{"alg":"HS256"}""")}.{B64(payload)}.sig";
    }

    [Fact]
    public void Meet_token_claims()
    {
        var claims = MeetToken.Decode(Token("""{"identity":"1840651530236071936","room":1840651530236071937,"exp":2000000000,"nbf":1000,"metadata":"Bot;"}"""))!;
        Assert.Equal("1840651530236071936", claims.UserId);
        Assert.Equal("1840651530236071937", claims.RoomId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(2_000_000_000), claims.ExpiresAt);
        Assert.Equal("Bot;", claims.Metadata);
        var livekit = MeetToken.Decode(Token("""{"sub":"42","video":{"room":"77"},"exp":1}"""))!;
        Assert.Equal(("42", "77"), (livekit.UserId, livekit.RoomId));
        foreach (var bad in new[] { "", "a.b", "a.!!.c", Token("[1]"), "a.b.c.d" })
        {
            Assert.Null(MeetToken.Decode(bad));
        }

        var now = DateTimeOffset.FromUnixTimeSeconds(1_999_999_000);
        Assert.False(MeetToken.NeedsRefresh(Token("""{"exp":2000000000}"""), now, TimeSpan.FromSeconds(60)));
        Assert.True(MeetToken.NeedsRefresh(Token("""{"exp":2000000000}"""), now.AddSeconds(950), TimeSpan.FromSeconds(60)));
        Assert.True(MeetToken.NeedsRefresh(Token("""{"identity":1}"""), now, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Meet_token_times_beyond_the_platform_count_as_missing()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_999_999_000);
        foreach (var huge in new[] { """{"exp":1e300}""", """{"exp":18446744073709551615,"nbf":1e20}""", """{"exp":8827734893628368701}""", """{"exp":-5}""" })
        {
            var claims = MeetToken.Decode(Token(huge))!;
            Assert.Null(claims.ExpiresAt);
            Assert.Null(claims.NotBefore);
            Assert.True(MeetToken.NeedsRefresh(Token(huge), now, TimeSpan.FromSeconds(60)));
        }
    }

    [Fact]
    public void Participant_metadata()
    {
        foreach (var (metadata, name, avatar) in new (string, string?, string?)[]
                 {
                     ("", null, null), ("Alice;https://cdn/a.png", "Alice", "https://cdn/a.png"), ("Bob;", "Bob", null), ("Carol", "Carol", null),
                     ("""{"username":"Dan","avatar":"x"}""", "Dan", "x"), ("{not json", "{not json", null),
                 })
        {
            Assert.Equal(new ParticipantInfo(name, avatar), ParticipantMetadata.Parse(metadata));
        }

        Assert.Equal("A,B;u", ParticipantMetadata.Format(new ParticipantInfo("A;B", "u")));
    }

    private static List<(string Name, int Number, string Type)> ProtoFields(string proto, string message)
    {
        var start = proto.IndexOf($"\nmessage {message} {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"message {message} not found");
        var body = proto.Substring(start + 1);
        body = body.Substring(0, body.IndexOf("\n}", StringComparison.Ordinal));
        var fields = new List<(string, int, string)>();
        foreach (var raw in body.Split('\n').Skip(1))
        {
            var line = raw.Trim();
            if (line.StartsWith("optional ", StringComparison.Ordinal))
            {
                line = line.Substring(9).Trim();
            }

            var eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var decl = line.Substring(0, eq).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (decl.Length < 2 || !int.TryParse(line.Substring(eq + 1).Trim().TrimEnd(';').Trim(), out var number))
            {
                continue;
            }

            fields.Add((decl[1], number, decl[0]));
        }

        return fields.OrderBy(f => f.Item1, StringComparer.Ordinal).ToList();
    }

    private static List<(string, int, string)> Want(IReadOnlyList<(string Name, int Number, string Type)> fields) =>
        fields.Select(f => (f.Name, f.Number, f.Type)).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

    [CanonicalProtoFact]
    public void Meet_token_messages_match_the_canonical_mezon_protocol()
    {
        var proto = File.ReadAllText(Path.Combine(CanonicalProtoFactAttribute.Dir!, "api", "api.proto")).Replace("\r\n", "\n");
        foreach (var (message, fields) in MeetTokenMessages.Schema)
        {
            Assert.Equal(Want(fields), ProtoFields(proto, message));
        }
    }

    [Fact]
    public void Meet_token_messages_agree_with_the_vendored_api_proto()
    {
        // Mezon.Net.Core vendors a hand-adjusted copy of api.proto that lags mezon-protocol: every field it has must agree.
        var proto = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "Mezon.Net.Core", "Protobuf", "Api", "api.proto")).Replace("\r\n", "\n");
        foreach (var (message, fields) in MeetTokenMessages.Schema)
        {
            var vendored = ProtoFields(proto, message);
            Assert.NotEmpty(vendored);
            Assert.All(vendored, field => Assert.Contains(field, Want(fields)));
        }
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Fact]
    public void Meet_token_protobuf_codec()
    {
        var request = new GenerateMeetTokenRequest("1840651530236071936", "0", "Bot;");
        Assert.Equal("08" + "80a080b888dad3c519" + "1201" + "30" + "1a04" + Hex(Encoding.UTF8.GetBytes("Bot;")), Hex(MeetTokenMessages.Encode(request)));
        Assert.Empty(MeetTokenMessages.Encode(new GenerateMeetTokenRequest("0")));
        Assert.Throws<MeetTokenProtobufException>(() => MeetTokenMessages.Encode(new GenerateMeetTokenRequest("abc")));
        Assert.Equal("0801100218ac02", Hex(MeetTokenMessages.Encode(new MeetParticipantRequest("1", "2", "300"))));
        Assert.Equal("08ffffffffffffffffff01", Hex(MeetTokenMessages.Encode(new MeetParticipantRequest("-1", "", ""))));

        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJpZGVudGl0eSI6MX0.sig";
        const string url = "wss://sfu.example/ws?x=1";
        var proto = new List<byte> { 0x0a, (byte)jwt.Length };
        proto.AddRange(Encoding.ASCII.GetBytes(jwt));
        proto.AddRange(new byte[] { 0x12, (byte)url.Length });
        proto.AddRange(Encoding.ASCII.GetBytes(url));
        Assert.Equal(new GenerateMeetTokenResponse(jwt, url), MeetTokenMessages.DecodeGenerateMeetTokenResponse(proto.ToArray()));
        Assert.Equal(jwt, MeetTokenMessages.DecodeGenerateMeetTokenResponse(Encoding.ASCII.GetBytes(jwt + "\n")).Token);
        Assert.Throws<MeetTokenProtobufException>(() => MeetTokenMessages.DecodeGenerateMeetTokenResponse(new byte[] { 0x12, 0x01, 0x41 }));
        Assert.Throws<MeetTokenProtobufException>(() => MeetTokenMessages.DecodeGenerateMeetTokenResponse(new byte[] { 0x0a, 0x09, 0x41 }));
        var external = new List<byte> { 0x0a, 0x03, (byte)'a', (byte)'.', (byte)'b', 0x10, 0x80, 0xa0, 0x80, 0xb8, 0x88, 0xda, 0xd3, 0xc5, 0x19 };
        external.AddRange(new byte[] { 0x1a, 0x01, (byte)'g', 0x22, 0x01, (byte)'u' });
        Assert.Equal(
            new GenerateMeetTokenExternalResponse("a.b", "1840651530236071936", "g", "u"),
            MeetTokenMessages.DecodeGenerateMeetTokenExternalResponse(external.ToArray()));
    }

    [Fact]
    public void Meet_token_protobuf_matches_the_generated_mezon_net_types()
    {
        var bytes = MeetTokenMessages.Encode(new GenerateMeetTokenRequest("1840651530236071936", "room", "Bot;https://a"));
        var parsed = Mezon.Net.Internal.Api.GenerateMeetTokenRequest.Parser.ParseFrom(bytes);
        Assert.Equal((1840651530236071936L, "room", "Bot;https://a"), (parsed.ChannelId, parsed.RoomName, parsed.Metadata));

        var participant = Mezon.Net.Internal.Api.MeetParticipantRequest.Parser.ParseFrom(MeetTokenMessages.Encode(new MeetParticipantRequest("7", "-8", "9")));
        Assert.Equal((7L, -8L, 9L), (participant.UserId, participant.ChannelId, participant.ClanId));

        var response = new Mezon.Net.Internal.Api.GenerateMeetTokenResponse { Token = "a.b.c-d", Url = "wss://sfu/ws" };
        var decoded = MeetTokenMessages.DecodeGenerateMeetTokenResponse(Google.Protobuf.MessageExtensions.ToByteArray(response));
        Assert.Equal(new GenerateMeetTokenResponse("a.b.c-d", "wss://sfu/ws"), decoded);
    }
}
