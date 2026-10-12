namespace Mezon.Net.Sdk.Sfu.Tests.Integration;

/// <summary>A room wired to a fake SFU and fake PeerConnections, recording its events as strings.</summary>
internal sealed class Harness : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly List<string> _events = new();
    private readonly List<string> _messages = new();
    private readonly Task _pump;

    private Harness(FakeSfu sfu, Room room)
    {
        Sfu = sfu;
        Room = room;
        _pump = Task.Run(Pump);
    }

    public FakeSfu Sfu { get; }

    public Room Room { get; }

    public static Harness Create(Func<RoomOptions, RoomOptions>? edit = null)
    {
        var sfu = FakeSfu.Start();
        var options = new RoomOptions
        {
            Reconnect = new ReconnectOptions { Delay = _ => TimeSpan.FromMilliseconds(10) },
            Timeouts = new Timeouts { Join = TimeSpan.FromSeconds(2), Admission = TimeSpan.FromSeconds(2), Operation = TimeSpan.FromSeconds(1) },
        };
        options = edit?.Invoke(options) ?? options;
        options = options with { PeerFactory = sfu.Factory() };
        return new Harness(sfu, new Room(options));
    }

    private async Task Pump()
    {
        await foreach (var e in Room.Events.ReadAllAsync())
        {
            string? line = e switch
            {
                ConnectionStateChanged s => $"state:{s.State}",
                Reconnecting => "reconnecting",
                Reconnected => "reconnected",
                Disconnected d => $"disconnected:{d.Reason}",
                ParticipantConnected p => $"joined:{p.Participant.PeerId}",
                ParticipantDisconnected p => $"left:{p.Participant.PeerId}",
                ParticipantUpdated p => $"updated:{p.Participant.PeerId}",
                TrackSubscribed t => $"sub:{t.Track.Mid}:{t.Track.Source}:{t.Participant.PeerId}",
                TrackUnsubscribed t => $"unsub:{t.Track.Mid}:{t.Track.Source}:{t.Participant.PeerId}",
                TrackMuted t => $"muted:{t.Track.Mid}",
                TrackUnmuted t => $"unmuted:{t.Track.Mid}",
                DownlinkModeDetected m => $"mode:{m.Mode}",
                LocalMuteForced => "forced-mute",
                SignalError s => $"signal-error:{s.Error.Code}",
                ActiveSpeakersChanged a => $"speakers:{string.Join(",", a.Speakers.Select(p => p.PeerId))}",
                _ => null,
            };
            lock (_lock)
            {
                if (e is MessageReceived m)
                {
                    _messages.Add($"{m.UserId}/{m.PeerId}/{m.Participant is not null}: {m.Message}");
                }
                else if (line is not null)
                {
                    _events.Add(line);
                }
            }
        }
    }

    public List<string> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToList();
            }
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

    public bool Has(string e) => Events.Contains(e);

    public int Count(string e) => Events.Count(x => x == e);

    public async Task Wait(string what, Func<bool> condition, double seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"timed out waiting for {what}; events [{string.Join(", ", Events)}]");
            }

            await Task.Delay(5);
        }
    }

    public Task WaitEvent(string e, double seconds = 5) => Wait(e, () => Has(e), seconds);

    public Task Connect(CancellationToken cancellationToken = default) =>
        Room.ConnectAsync(Sfu.Url, TokenProvider.Fixed(FakeSfu.Token("7", 3600)), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Room.DisposeAsync();
        await Task.WhenAny(_pump, Task.Delay(2000));
        await Sfu.DisposeAsync();
    }
}
