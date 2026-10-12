using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mezon.Net.Sdk.Sfu.Tests.E2e;

/// <summary>A real-SFU stack started by <c>dotnet run --project tools/SfuE2e -- up</c>.</summary>
internal sealed class Stack
{
    private readonly string _secretFile;

    private Stack(string mode, string wsUrl, string secretFile, bool loopback)
    {
        Mode = mode;
        WsUrl = wsUrl;
        _secretFile = secretFile;
        Loopback = loopback;
    }

    public string Mode { get; }

    public string WsUrl { get; }

    /// <summary>The SFU media port is published on 127.0.0.1 (host-network stack).</summary>
    public bool Loopback { get; }

    /// <summary>
    /// The stack of <paramref name="mode"/>, or null when it is not running or not reachable from here. Host-network
    /// stacks are reachable from the machine that started them; bridge stacks from Linux hosts and containers on
    /// that network. <c>SFU_E2E_NET</c> (host, bridge or none) restricts which are used.
    /// </summary>
    public static Stack? Get(string mode)
    {
        var path = Path.Combine(TestPaths.RepoRoot, "tools", "SfuE2e", "stack", ".runtime", $"{mode}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var net = root.GetProperty("net").GetString();
        var only = Environment.GetEnvironmentVariable("SFU_E2E_NET");
        var usable = only is { Length: > 0 } ? net == only : net == "host" || OperatingSystem.IsLinux();
        if (!usable)
        {
            return null;
        }

        var secret = Path.Combine(TestPaths.RepoRoot, root.GetProperty("jwtSecretFile").GetString()!);
        return File.Exists(secret) ? new Stack(mode, root.GetProperty("wsUrl").GetString()!, secret, net == "host") : null;
    }

    /// <summary>The same stack reached through <paramref name="proxy"/>.</summary>
    public Stack Via(Proxy proxy) => new(Mode, proxy.Url, _secretFile, Loopback);

    /// <summary>Signs a meet token with the stack's secret (never print it).</summary>
    public string Token(string userId, string roomId, string metadata, TimeSpan ttl)
    {
        var secret = File.ReadAllText(_secretFile).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var head = B64(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var body = B64(Encoding.UTF8.GetBytes(
            $"{{\"identity\":\"{userId}\",\"room\":\"{roomId}\",\"exp\":{now + (long)ttl.TotalSeconds},\"nbf\":{now - 30},\"metadata\":{JsonSerializer.Serialize(metadata)}}}"));
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"{head}.{body}.{B64(mac.ComputeHash(Encoding.UTF8.GetBytes($"{head}.{body}")))}";
    }
}

/// <summary>Skipped unless every stack in <c>modes</c> runs and is reachable from here.</summary>
public sealed class E2eFactAttribute : FactAttribute
{
    public E2eFactAttribute(params string[] modes)
    {
        var missing = modes.Where(m => Stack.Get(m) is null).ToList();
        if (missing.Count > 0)
        {
            Skip = $"the {string.Join(", ", missing)} stack is not running (dotnet run --project tools/SfuE2e -- up --mode {missing[0]})";
        }
    }
}

/// <summary>The real-SFU tests run one at a time: they create many PeerConnections and measure media flow.</summary>
[CollectionDefinition("e2e", DisableParallelization = true)]
public sealed class E2eCollection
{
}

/// <summary>A room plus what it saw.</summary>
internal sealed class Peer : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly List<string> _events = new();
    private readonly Dictionary<(string, byte), int> _frames = new();
    private readonly Dictionary<(string, byte), (uint First, uint Last)> _sequences = new();
    private readonly List<string> _messages = new();
    private readonly CancellationTokenSource _stop = new();
    private CancellationTokenSource _frameStop = new();

    public Peer(Room room, string userId)
    {
        Room = room;
        UserId = userId;
        _ = Task.Run(Watch);
    }

    public Room Room { get; }

    public string UserId { get; }

    /// <summary>A random 19-digit id (beyond 2^53 on purpose).</summary>
    public static string RandomId() =>
        $"{Random.Shared.Next(1, 9)}{Random.Shared.NextInt64(0, 1_000_000_000_000_000_000):D18}";

    /// <summary>A valid Opus packet (CELT fullband 20 ms, mono) carrying a marker and a sequence number; the SFU forwards Opus payloads unchanged.</summary>
    public static byte[] OpusFrame(byte marker, uint seq)
    {
        var frame = new byte[60];
        frame[0] = 0xF8;
        frame[1] = (byte)'M';
        frame[2] = marker;
        frame[3] = (byte)(seq >> 24);
        frame[4] = (byte)(seq >> 16);
        frame[5] = (byte)(seq >> 8);
        frame[6] = (byte)seq;
        for (var i = 7; i < frame.Length; i++)
        {
            frame[i] = (byte)(i ^ marker);
        }

        return frame;
    }

    public static (byte Marker, uint Seq)? ParseFrame(ReadOnlySpan<byte> payload) =>
        payload.Length == 60 && payload[0] == 0xF8 && payload[1] == (byte)'M'
            ? (payload[2], (uint)(payload[3] << 24 | payload[4] << 16 | payload[5] << 8 | payload[6]))
            : null;

    private async Task Watch()
    {
        try
        {
            await foreach (var e in Room.Events.ReadAllAsync(_stop.Token))
            {
                string? line = e switch
                {
                    TrackSubscribed t => $"subscribed:{t.Track.Source}:{t.Participant.UserId}",
                    TrackUnsubscribed t => $"unsubscribed:{t.Track.Source}:{t.Participant.UserId}",
                    ParticipantConnected p => $"joined:{p.Participant.UserId}",
                    ParticipantDisconnected p => $"left:{p.Participant.UserId}",
                    Reconnecting => "reconnecting",
                    Reconnected => "reconnected",
                    Disconnected d => $"disconnected:{d.Reason}",
                    LocalMuteForced => "forced-mute",
                    _ => null,
                };
                if (e is TrackSubscribed subscribed)
                {
                    var track = subscribed.Track;
                    var user = subscribed.Participant.UserId;
                    _ = Task.Run(async () =>
                    {
                        await foreach (var packet in track.ReadAllAsync(_stop.Token))
                        {
                            if (ParseFrame(packet.Payload.Span) is { } frame)
                            {
                                lock (_lock)
                                {
                                    _frames[(user, frame.Marker)] = _frames.GetValueOrDefault((user, frame.Marker)) + 1;
                                    _sequences[(user, frame.Marker)] = _sequences.TryGetValue((user, frame.Marker), out var range) ? (range.First, frame.Seq) : (frame.Seq, frame.Seq);
                                }
                            }
                        }
                    });
                }

                lock (_lock)
                {
                    if (e is MessageReceived m)
                    {
                        _messages.Add($"{m.UserId}: {m.Message}");
                    }
                    else if (line is not null)
                    {
                        _events.Add(line);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public int Frames(string userId, byte marker)
    {
        lock (_lock)
        {
            return _frames.GetValueOrDefault((userId, marker));
        }
    }

    public (uint First, uint Last)? SequenceRange(string userId, byte marker)
    {
        lock (_lock)
        {
            return _sequences.TryGetValue((userId, marker), out var r) ? r : null;
        }
    }

    public bool Has(string e)
    {
        lock (_lock)
        {
            return _events.Contains(e);
        }
    }

    public List<string> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    /// <summary>Enables the microphone and writes marked frames every 20 ms with an audio level from <paramref name="level"/>.</summary>
    public async Task Publish(byte marker, Func<byte?> level)
    {
        await Room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        StartFrames(marker, level);
    }

    public void StartFrames(byte marker, Func<byte?> level)
    {
        var mic = Room.LocalParticipant.Microphone;
        var stop = _frameStop.Token;
        _ = Task.Run(async () =>
        {
            var clock = Stopwatch.StartNew();
            var due = TimeSpan.Zero;
            for (uint seq = 0; !stop.IsCancellationRequested; seq++)
            {
                mic.WriteOpus(OpusFrame(marker, seq), TimeSpan.FromMilliseconds(20), level());
                due += TimeSpan.FromMilliseconds(20);
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(wait, stop);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        });
    }

    public void StopFrames()
    {
        _frameStop.Cancel();
        _frameStop = new CancellationTokenSource();
    }

    public async ValueTask DisposeAsync()
    {
        _frameStop.Cancel();
        await Room.DisposeAsync();
        _stop.Cancel();
    }
}

internal static class E2e
{
    public static RoomOptions Options(Role role = Role.Speaker) => new()
    {
        Role = role,
        Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromMilliseconds(200) },
    };

    public static async Task<(Peer Peer, Exception? Error)> TryJoin(Stack stack, string roomId, string name, RoomOptions? options = null, string? userId = null, TokenProvider? tokens = null)
    {
        userId ??= Peer.RandomId();
        options ??= Options();

        // Host-mode stacks publish the SFU on 127.0.0.1; bind the ICE socket there (on Windows a socket bound to
        // another interface cannot reach loopback). Tests pass no STUN server.
        options = options with
        {
            BindAddress = stack.Loopback ? IPAddress.Loopback : options.BindAddress,
            IceServersPolicy = IceServersPolicy.Override,
        };
        var room = new Room(options);
        var peer = new Peer(room, userId);
        tokens ??= TokenProvider.Fixed(stack.Token(userId, roomId, $"{name};", TimeSpan.FromMinutes(10)));
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await room.ConnectAsync(stack.WsUrl, tokens, timeout.Token);
            return (peer, null);
        }
        catch (Exception e)
        {
            return (peer, e);
        }
    }

    public static async Task<Peer> Join(Stack stack, string roomId, string name, RoomOptions? options = null, string? userId = null, TokenProvider? tokens = null)
    {
        var (peer, error) = await TryJoin(stack, roomId, name, options, userId, tokens);
        if (error is not null)
        {
            await peer.DisposeAsync();
            throw new InvalidOperationException($"{name}: connect failed: {error.Message}", error);
        }

        return peer;
    }

    /// <summary>Polls <paramref name="condition"/> until true or <paramref name="seconds"/> elapse.</summary>
    public static async Task WaitFor(double seconds, string what, Func<bool> condition)
    {
        if (!await Poll(seconds, condition))
        {
            Assert.Fail($"timed out waiting for {what}");
        }
    }

    public static async Task<bool> Poll(double seconds, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(50);
        }

        return true;
    }
}

/// <summary>
/// A TCP proxy in front of the SFU's signaling port, so a test can drop a room's socket without a close frame (the
/// client sees an abnormal closure) and count connections.
/// </summary>
internal sealed class Proxy : IDisposable
{
    private readonly TcpListener _listener;
    private readonly List<(TcpClient Client, TcpClient? Server)> _connections = new();
    private readonly object _lock = new();
    private readonly string _target;
    private readonly int _targetPort;

    private Proxy(Stack stack)
    {
        var uri = new Uri(stack.WsUrl);
        _target = uri.Host;
        _targetPort = uri.Port;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Url = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/ws";
        _ = Task.Run(Accept);
    }

    public static Proxy Start(Stack stack) => new(stack);

    public string Url { get; }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _connections.Count;
            }
        }
    }

    private async Task Accept()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                return;
            }

            var index = 0;
            lock (_lock)
            {
                _connections.Add((client, null));
                index = _connections.Count - 1;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var server = new TcpClient();
                    await server.ConnectAsync(_target, _targetPort);
                    lock (_lock)
                    {
                        _connections[index] = (client, server);
                    }

                    var a = client.GetStream().CopyToAsync(server.GetStream());
                    var b = server.GetStream().CopyToAsync(client.GetStream());
                    await Task.WhenAny(a, b);
                }
                catch (Exception)
                {
                }
                finally
                {
                    Abort(client);
                    lock (_lock)
                    {
                        Abort(_connections[index].Server);
                    }
                }
            });
        }
    }

    private static void Abort(TcpClient? tcp)
    {
        if (tcp is null)
        {
            return;
        }

        try
        {
            tcp.Client.LingerState = new LingerOption(true, 0);
            tcp.Close();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Drops the newest connection (both sides, without close frames).</summary>
    public void Cut()
    {
        lock (_lock)
        {
            if (_connections.Count > 0)
            {
                Abort(_connections[^1].Client);
                Abort(_connections[^1].Server);
            }
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        lock (_lock)
        {
            foreach (var (client, server) in _connections)
            {
                Abort(client);
                Abort(server);
            }
        }
    }
}
