using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;

namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

/// <summary>One client connection as seen by the fake server.</summary>
internal sealed class Conn
{
    private readonly Channel<object> _out = Channel.CreateUnbounded<object>();
    private readonly TcpClient _tcp;
    private readonly object _lock = new();
    private readonly List<JsonObject> _received = new();

    public Conn(TcpClient tcp) => _tcp = tcp;

    public int? ClientClose { get; set; }

    public uint PeerId { get; set; }

    public string UserId { get; set; } = "";

    public string Role { get; set; } = "speaker";

    public string Metadata { get; set; } = "";

    public bool IsMute { get; set; } = true;

    public bool Camera { get; set; }

    public int Answered { get; set; }

    public long Generation { get; set; } = -1;

    /// <summary>The pools of a decoupled room, shaping every offer.</summary>
    public MediaPoolLayout? Pool { get; set; }

    public bool Ended { get; set; }

    public ChannelReader<object> Outgoing => _out.Reader;

    public void Record(JsonObject message)
    {
        lock (_lock)
        {
            _received.Add(message);
        }
    }

    public void Send(object message) => _out.Writer.TryWrite(message is string s ? s : JsonSerializer.Serialize(message));

    public void SendRaw(string frame) => _out.Writer.TryWrite(frame);

    /// <summary>Sends a text frame with arbitrary bytes (for example invalid UTF-8).</summary>
    public void SendTextBytes(byte[] frame) => _out.Writer.TryWrite(frame);

    public void Close(int code) => _out.Writer.TryWrite(code);

    /// <summary>Cuts the TCP connection without a close frame.</summary>
    public void DropNow()
    {
        try
        {
            if (_tcp.Client is { } socket)
            {
                socket.LingerState = new LingerOption(true, 0);
                socket.Close();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public long Offer(IReadOnlyList<FixtureSlot> slots, long? generation = null)
    {
        long g;
        bool audience;
        lock (_lock)
        {
            g = generation ?? Generation + 1;
            Generation = Math.Max(Generation, g);
            audience = Role == "audience";
        }

        Send(new { type = "offer", offer_generation = g, sdp = OfferFixtures.BuildOffer(new OfferOptions(Audience: audience, Slots: slots, Pool: Pool)) });
        return g;
    }

    public object Own() => new
    {
        peer_id = PeerId,
        user_id = UserId,
        metadata = Metadata,
        role = Role,
        is_mute = IsMute,
        camera_requested = Camera,
        camera_active = Camera,
        screen_requested = false,
        screen_active = false,
    };

    public List<JsonObject> Messages(string type)
    {
        lock (_lock)
        {
            return _received.Where(m => m["type"]?.GetValue<string>() == type).ToList();
        }
    }

    public List<string> Types()
    {
        lock (_lock)
        {
            return _received.Select(m => m["type"]?.GetValue<string>() ?? "").ToList();
        }
    }

    public List<long> Answers() => Messages("answer").Select(m => m["offer_generation"]!.GetValue<long>()).ToList();
}

/// <summary>
/// A scripted mezon-sfu on a real WebSocket: joined then offer generation 0 on join, a snapshot after the first
/// answer, acknowledgements for control messages, and a client close answered with 4008. With <see cref="Pool"/> set,
/// a decoupled room: it rejects a join without <c>media_pool_version: 1</c> like mezon-sfu (error, then close 1002).
/// </summary>
internal sealed class FakeSfu : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly object _lock = new();
    private readonly List<Conn> _conns = new();
    private readonly List<FakePeer> _peers = new();
    private readonly Dictionary<string, List<Func<Conn, JsonObject, bool>>> _handlers = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly List<string> _requestLines = new();
    private uint _nextPeer = 100;
    private int _factoryCalls;

    private FakeSfu()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Url = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/ws";
        _ = Task.Run(AcceptLoop);
    }

    public static FakeSfu Start() => new();

    public string Url { get; }

    public List<FixtureSlot> Slots { get; set; } = new();

    public MediaPoolLayout? Pool { get; set; }

    public List<object> Members { get; set; } = new();

    public bool Refuse { get; set; }

    public bool SilentJoin { get; set; }

    public int FailOffers { get; set; }

    public bool NeverConnect { get; set; }

    /// <summary>PeerConnections whose <c>ReceivingMids</c> throws (counted down per PeerConnection created).</summary>
    public int BrokenPeers { get; set; }

    /// <summary>When set, the PeerConnection factory waits for it before creating the PeerConnection.</summary>
    public Task? CreateGate { get; set; }

    public int FactoryCalls
    {
        get
        {
            lock (_lock)
            {
                return _factoryCalls;
            }
        }
    }

    public int ConnCount
    {
        get
        {
            lock (_lock)
            {
                return _conns.Count;
            }
        }
    }

    public Conn First
    {
        get
        {
            lock (_lock)
            {
                return _conns.First();
            }
        }
    }

    public Conn Current
    {
        get
        {
            lock (_lock)
            {
                return _conns.Last();
            }
        }
    }

    public FakePeer Peer
    {
        get
        {
            lock (_lock)
            {
                return _peers.Last();
            }
        }
    }

    public int PeerCount
    {
        get
        {
            lock (_lock)
            {
                return _peers.Count;
            }
        }
    }

    public List<FakePeer> Peers
    {
        get
        {
            lock (_lock)
            {
                return _peers.ToList();
            }
        }
    }

    /// <summary>The HTTP request line of every WebSocket handshake.</summary>
    public List<string> RequestLines
    {
        get
        {
            lock (_lock)
            {
                return _requestLines.ToList();
            }
        }
    }

    public void On(string type, Func<Conn, JsonObject, bool> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Func<Conn, JsonObject, bool>>();
                _handlers[type] = list;
            }

            list.Add(handler);
        }
    }

    public static object Member(uint peer) => new
    {
        peer_id = peer,
        user_id = (9000 + peer).ToString(),
        metadata = $"Peer{peer};",
        role = "speaker",
        is_mute = false,
        camera_requested = false,
        camera_active = false,
        screen_requested = false,
        screen_active = false,
        ufrag = $"u{peer}",
        mid_audio = 0,
        mid_video = 0,
        mid_screen = 0,
        slot = 4294967295u,
        assignment_generation = 0,
    };

    /// <summary>A member of a decoupled room holding the given <c>media_slots</c> (see <see cref="PoolSlot"/>).</summary>
    public static JsonObject PoolMember(uint peer, JsonObject? audio = null, JsonObject? camera = null, JsonObject? screen = null)
    {
        var node = MemberWith(peer, m =>
        {
            foreach (var key in new[] { "mid_audio", "mid_video", "mid_screen", "slot", "assignment_generation" })
            {
                m.Remove(key);
            }
        });
        node["media_slots"] = new JsonObject { ["audio"] = audio, ["camera"] = camera, ["screen"] = screen };
        return node;
    }

    public static JsonObject PoolSlot(uint slot, long generation, uint mid) => new() { ["slot"] = slot, ["generation"] = generation, ["mid"] = mid };

    /// <summary>A member with fields replaced, e.g. <c>MemberWith(2, camera_active: true)</c>.</summary>
    public static JsonObject MemberWith(uint peer, Action<JsonObject> edit)
    {
        var node = JsonSerializer.SerializeToNode(Member(peer))!.AsObject();
        edit(node);
        return node;
    }

    public static string Token(string user, long expiresIn)
    {
        var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
        return ProtocolTests.Token(JsonSerializer.Serialize(new { identity = user, room = "42", exp, metadata = "Me;" }));
    }

    public void Snapshot(Conn conn)
    {
        var own = JsonSerializer.SerializeToNode(conn.Own())!.AsObject();
        own["ufrag"] = "self";
        own["slot"] = 4294967295u;
        List<object> members;
        lock (_lock)
        {
            members = Members.ToList();
        }

        var all = new List<object> { own };
        all.AddRange(members);
        conn.Send(new { type = "room_snapshot", room = "42", room_revision = 1, self_peer_id = conn.PeerId, participant_count = all.Count, members = all });
    }

    public Func<PeerConfig, CancellationToken, Task<IPeerConnection>> Factory() => async (config, _) =>
    {
        Task? gate;
        lock (_lock)
        {
            _factoryCalls++;
            gate = CreateGate;
        }

        if (gate is not null)
        {
            await gate;
        }

        var peer = new FakePeer(config, this);
        lock (_lock)
        {
            peer.Ufrag = $"cli{_peers.Count}";
            peer.Broken = BrokenPeers > 0;
            BrokenPeers = Math.Max(0, BrokenPeers - 1);
            _peers.Add(peer);
        }

        return peer;
    };

    internal bool TakeOfferFailure()
    {
        lock (_lock)
        {
            if (FailOffers > 0)
            {
                FailOffers--;
                return true;
            }

            return false;
        }
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => Serve(tcp));
        }
    }

    private async Task<bool> Handshake(NetworkStream stream)
    {
        var buffer = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(one) == 0)
            {
                return false;
            }

            buffer.Add(one[0]);
            if (buffer.Count >= 4 && buffer[^4] == '\r' && buffer[^3] == '\n' && buffer[^2] == '\r' && buffer[^1] == '\n')
            {
                break;
            }
        }

        var request = Encoding.ASCII.GetString(buffer.ToArray());
        var lines = request.Split("\r\n");
        lock (_lock)
        {
            _requestLines.Add(lines[0]);
        }

        var keyLine = lines.FirstOrDefault(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
        if (keyLine is null)
        {
            return false;
        }

        var key = keyLine.Substring(keyLine.IndexOf(':') + 1).Trim();
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        return true;
    }

    private async Task Serve(TcpClient tcp)
    {
        bool refuse;
        lock (_lock)
        {
            refuse = Refuse;
        }

        if (refuse)
        {
            tcp.Close();
            return;
        }

        var stream = tcp.GetStream();
        if (!await Handshake(stream))
        {
            tcp.Close();
            return;
        }

        using var ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
        var conn = new Conn(tcp);
        lock (_lock)
        {
            _conns.Add(conn);
        }

        var writer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in conn.Outgoing.ReadAllAsync(_stop.Token))
                {
                    if (item is int code)
                    {
                        await ws.CloseOutputAsync((WebSocketCloseStatus)code, "", CancellationToken.None);
                        return;
                    }

                    var bytes = item as byte[] ?? Encoding.UTF8.GetBytes((string)item);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
        });

        var buffer = new byte[1 << 20];
        try
        {
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, _stop.Token);
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    conn.ClientClose = (int?)ws.CloseStatus;
                    conn.Close(4008);
                    break;
                }

                var text = Encoding.UTF8.GetString(message.ToArray());
                var value = JsonNode.Parse(text)!.AsObject();
                conn.Record(value);
                Handle(conn, value);
            }
        }
        catch (Exception)
        {
        }

        conn.Ended = true;
        await Task.WhenAny(writer, Task.Delay(1000));
        tcp.Close();
    }

    private void Handle(Conn conn, JsonObject m)
    {
        var type = m["type"]?.GetValue<string>() ?? "";
        List<Func<Conn, JsonObject, bool>> handlers;
        lock (_lock)
        {
            handlers = _handlers.TryGetValue(type, out var list) ? list.ToList() : new List<Func<Conn, JsonObject, bool>>();
        }

        if (handlers.Any(h => h(conn, m)))
        {
            return;
        }

        switch (type)
        {
            case "join":
            {
                var token = m["token"]?.GetValue<string>() ?? "";
                var claims = MeetToken.Decode(token);
                if (claims?.UserId is null)
                {
                    conn.Send(new { type = "error", message = "invalid_token" });
                    conn.Close(4005);
                    return;
                }

                bool silent;
                List<FixtureSlot> slots;
                MediaPoolLayout? pool;
                uint peer;
                lock (_lock)
                {
                    peer = _nextPeer++;
                    silent = SilentJoin;
                    slots = Slots.ToList();
                    pool = Pool;
                }

                if (pool is not null && m["media_pool_version"]?.GetValue<int>() != 1)
                {
                    conn.Send(new { type = "error", message = "unsupported_media_pool_version" });
                    conn.Close(1002);
                    return;
                }

                conn.UserId = claims.UserId;
                conn.Metadata = claims.Metadata ?? "";
                conn.PeerId = peer;
                if (m["role"]?.GetValue<string>() == "audience")
                {
                    conn.Role = "audience";
                }

                if (silent)
                {
                    return;
                }

                var joined = JsonSerializer.SerializeToNode(new { type = "joined", room = "42", peer_id = peer, metadata = "", iceServers = new[] { new { urls = "stun:stun.example:3478" } } })!.AsObject();
                if (pool is not null)
                {
                    joined["media_pool_version"] = 1;
                    joined["media_pool_mode"] = "decoupled";
                    joined["max_audio_slots"] = pool.Audio;
                    joined["max_camera_slots"] = pool.Camera;
                    joined["max_screen_slots"] = pool.Screen;
                }

                conn.Send(joined.ToJsonString());
                conn.Pool = pool;
                conn.Offer(slots, 0);
                break;
            }

            case "answer":
                conn.Answered++;
                if (conn.Answered == 1)
                {
                    Snapshot(conn);
                }

                break;
            case "mute":
            {
                var muted = m["is_mute"]?.GetValue<bool>() == true;
                conn.IsMute = muted;
                var reply = new JsonObject { ["type"] = "mute_changed", ["is_mute"] = muted };
                if (m["mute_revision"] is { } revision)
                {
                    reply["mute_revision"] = revision.GetValue<long>();
                }

                conn.Send(reply.ToJsonString());
                if (conn.Answered > 0)
                {
                    conn.Send(new { type = "peer_updated", peer = conn.Own() });
                }

                break;
            }

            case "camera":
                if (conn.Role == "audience")
                {
                    conn.Send(new { type = "error", message = "camera_not_allowed" });
                    return;
                }

                conn.Camera = m["active"]?.GetValue<bool>() == true;
                conn.Send(new { type = "peer_updated", peer = conn.Own() });
                break;
            case "push_to_talk":
                if (conn.Role != "audience")
                {
                    conn.Send(new { type = "error", message = "push_to_talk_rejected" });
                    return;
                }

                conn.Send(new { type = "push_to_talk_changed", active = m["active"]?.GetValue<bool>() == true });
                break;
            case "visibility":
            {
                var camera = m["visible_camera"]?.GetValue<bool>() != false;
                var screen = m["visible_screen"]?.GetValue<bool>() != false;
                conn.Send(new { type = "visibility_changed", visible = camera || screen, camera, screen });
                break;
            }

            case "send_message":
                conn.Send(new { type = "message_sent" });
                break;
            case "peer_update":
                conn.Metadata = m["metadata"]?.GetValue<string>() ?? "";
                conn.Send(new { type = "peer_updated", peer = conn.Own() });
                break;
            case "participant_action":
                conn.Send(new { type = "participant_action_completed", action = "mute", user_id = "7", affected = 1 });
                break;
            case "request_keyframe":
                conn.Send(new { type = "keyframe_requested", success = true, cached = false, kind = m["kind"]?.GetValue<string>(), publisher_id = m["publisher_id"]?.GetValue<long>() });
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        List<Conn> conns;
        lock (_lock)
        {
            conns = _conns.ToList();
        }

        foreach (var c in conns)
        {
            c.DropNow();
        }

        await Task.Yield();
    }
}

/// <summary>Answers like a browser would, without media; connects after the first answer.</summary>
internal sealed class FakePeer : IPeerConnection
{
    public const string BrokenMessage = "fake: the WebRTC stack failed";

    private readonly FakeSfu _sfu;
    private readonly object _lock = new();
    private string _remote = "";
    private string _local = "";
    private bool _connected;

    public FakePeer(PeerConfig config, FakeSfu sfu)
    {
        Config = config;
        _sfu = sfu;
    }

    public PeerConfig Config { get; }

    public string Ufrag { get; set; } = "cli";

    public bool Closed { get; private set; }

    /// <summary>Makes <see cref="ReceivingMids"/> throw, like a WebRTC stack failing unexpectedly.</summary>
    public bool Broken { get; set; }

    public Task SetRemoteOfferAsync(string sdp)
    {
        if (_sfu.TakeOfferFailure())
        {
            throw new InvalidOperationException("fake: offer rejected");
        }

        lock (_lock)
        {
            _remote = sdp;
        }

        return Task.CompletedTask;
    }

    public Task<string> CreateAnswerAsync()
    {
        string remote;
        lock (_lock)
        {
            remote = _remote;
        }

        var lines = new List<string> { "v=0", "o=- 2 2 IN IP4 0.0.0.0", "s=-", "t=0 0", $"a=ice-ufrag:{Ufrag}" };
        foreach (var s in Sdp.Parse(remote).Sections)
        {
            var direction = s.Direction switch
            {
                "recvonly" when s.Mid == "1" && !Config.Camera => "inactive",
                "recvonly" => "sendonly",
                "sendonly" => "recvonly",
                { } d => d,
                null => "inactive",
            };
            lines.Add($"m={s.Media} 9 UDP/TLS/RTP/SAVPF 96");
            lines.Add($"a=mid:{s.Mid}");
            lines.Add($"a={direction}");
        }

        return Task.FromResult(string.Join("\r\n", lines) + "\r\n");
    }

    public Task SetLocalAnswerAsync(string sdp)
    {
        bool connect;
        lock (_lock)
        {
            _local = sdp;
            connect = !_connected && !_sfu.NeverConnect;
            _connected |= connect;
        }

        if (connect)
        {
            _ = Task.Run(() => Config.OnState(PeerState.Connected));
        }

        return Task.CompletedTask;
    }

    public Task RollbackAsync() => Task.CompletedTask;

    public string LocalSdp
    {
        get
        {
            lock (_lock)
            {
                return _local;
            }
        }
    }

    public IReadOnlyCollection<uint> ReceivingMids()
    {
        if (Broken)
        {
            throw new InvalidOperationException(BrokenMessage);
        }

        lock (_lock)
        {
            return Sdp.Parse(_local).Sections
                .Where(s => s.Direction == "recvonly")
                .Select(s => uint.Parse(s.Mid!))
                .Where(mid => mid >= 3)
                .ToList();
        }
    }

    public (IAudioUplink? Microphone, IVideoUplink? Camera) Uplinks() => (Microphone, Config.Camera ? Camera : null);

    public FakeAudioUplink Microphone { get; } = new();

    public FakeVideoUplink Camera { get; } = new();

    public Task CloseAsync()
    {
        Closed = true;
        return Task.CompletedTask;
    }

    public void Fail() => Config.OnState(PeerState.Failed);

    public void Disconnect() => Config.OnState(PeerState.Disconnected);

    /// <summary>Delivers one RTP packet on a remote mid.</summary>
    public void Deliver(uint mid, ushort sequence, string mime = "audio/opus") =>
        Config.OnRtp(mid, new RtpPacket(111, sequence, sequence * 960u, 1234, false, new byte[] { 0xF8, (byte)sequence }), mime);
}

internal sealed class FakeAudioUplink : IAudioUplink
{
    private readonly object _lock = new();

    public List<(byte[] Frame, uint Timestamp, bool Marker, byte Level, bool Voice)> Sent { get; } = new();

    public void SendOpus(byte[] frame, uint timestamp, bool marker, byte level, bool voice)
    {
        lock (_lock)
        {
            Sent.Add((frame, timestamp, marker, level, voice));
        }
    }
}

internal sealed class FakeVideoUplink : IVideoUplink
{
    public int Frames { get; private set; }

    public void SendVp8(byte[] frame, uint durationRtpUnits) => Frames++;
}
