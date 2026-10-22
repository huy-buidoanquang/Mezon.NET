using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>
/// A Mezon SFU room. Create one per meeting, <see cref="ConnectAsync"/> it, publish with
/// <see cref="LocalParticipant"/> and receive with <see cref="TrackSubscribed"/> events from <see cref="Events"/>.
/// Dispose it (or call <see cref="DisconnectAsync"/>) to leave: the socket, the PeerConnection and every background
/// task are released then, not when the object is garbage collected. Thread-safe.
/// </summary>
/// <remarks>
/// Concurrency: one lock guards the room state and is never held across an await. Socket frames (reader task),
/// timers (a 200 ms tick task per session), negotiation (one task per session) and WebRTC callbacks take it briefly;
/// events are queued under it, so they are delivered in order. PeerConnection calls happen outside the lock and
/// their results are applied under it. Remote RTP goes from the WebRTC receive thread to the subscribed track's
/// buffer without taking the lock.
/// </remarks>
public sealed class Room : IAsyncDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(200);
    private static long _sessionIds;

    private readonly object _gate = new();
    private readonly RoomOptions _options;
    private readonly ILogger _logger;
    private readonly Channel<RoomEvent> _events = Channel.CreateUnbounded<RoomEvent>();
    private readonly LocalAudioTrack _microphone = new();
    private readonly LocalVideoTrack _camera = new();
    private readonly Membership _membership = new();
    private readonly SlotMap _slots = new();
    private readonly DynamicOccupancy _dynamic = new();
    private readonly SortedDictionary<uint, RemoteParticipant> _participants = new();
    private readonly SortedDictionary<uint, TrackSpec> _published = new();
    private readonly SortedDictionary<uint, RemoteTrack> _publishedTracks = new();
    private readonly ReconnectPolicy _policy;
    private readonly SpeakerDetector _detector;
    private readonly List<Task> _closing = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private SessionState? _session;
    private SessionEnd? _lastEnd;
    private bool _micEnabled;
    private bool _cameraEnabled;
    private bool _pushToTalk;
    private string? _metadata;
    private uint _localPeerId;
    private string? _localUserId;
    private ParticipantState? _serverState;
    private DownlinkMode _mode = DownlinkMode.Unknown;
    private uint _slotCount;
    private MediaPools? _pools;
    private string _url = "";
    private TokenProvider? _tokens;
    private string? _token;
    private bool _tokenRejected;
    private List<RemoteParticipant> _speakers = new();
    private string? _roomId;
    private VideoReceiving _visibility = new(true, true);
    private long _connectGen;
    private CancellationTokenSource _timers = new();
    private bool _disposed;
    private Task? _disposing;

    public Room(RoomOptions? options = null)
    {
        _options = options ?? new RoomOptions();
        _logger = (ILogger?)_options.LoggerFactory?.CreateLogger("Mezon.Net.Sdk.Sfu") ?? NullLogger.Instance;
        _policy = new ReconnectPolicy(new ReconnectLimits(_options.Reconnect.Enabled, _options.Reconnect.MaxAttempts, _options.Reconnect.MaxDuration));
        _detector = new SpeakerDetector(_options.ActiveSpeakers);
        LocalParticipant = new LocalParticipant(this);
    }

    /// <summary>The event stream, in order. Unbounded: read it, or events accumulate.</summary>
    public ChannelReader<RoomEvent> Events => _events.Reader;

    public ConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>The room id from the meet token (decimal int64).</summary>
    public string? RoomId
    {
        get
        {
            lock (_gate)
            {
                return _roomId;
            }
        }
    }

    /// <summary>How the server maps remote publishers, known after the first offer with a publisher (decoupled rooms: on joining).</summary>
    public DownlinkMode DownlinkMode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    /// <summary>The server's participant count, this client included.</summary>
    public int ParticipantCount
    {
        get
        {
            lock (_gate)
            {
                return _membership.ParticipantCount;
            }
        }
    }

    public LocalParticipant LocalParticipant { get; }

    /// <summary>The other participants, ordered by peer id.</summary>
    public IReadOnlyList<RemoteParticipant> RemoteParticipants
    {
        get
        {
            lock (_gate)
            {
                return _participants.Values.ToList();
            }
        }
    }

    /// <summary>The participants currently speaking, loudest first.</summary>
    public IReadOnlyList<RemoteParticipant> ActiveSpeakers
    {
        get
        {
            lock (_gate)
            {
                return _speakers.ToList();
            }
        }
    }

    public RemoteParticipant? GetParticipant(uint peerId)
    {
        lock (_gate)
        {
            return _participants.TryGetValue(peerId, out var p) ? p : null;
        }
    }

    /// <summary>Every session of <paramref name="userId"/> in the room.</summary>
    public IReadOnlyList<RemoteParticipant> GetParticipantsByUserId(string userId) =>
        RemoteParticipants.Where(p => p.UserId == userId).ToList();

    internal LocalAudioTrack Microphone => _microphone;

    internal LocalVideoTrack Camera => _camera;

    internal RoomOptions Options => _options;

    internal object Gate => _gate;

    /// <summary>
    /// Joins the room at <paramref name="url"/> (the SFU WebSocket URL; the token is never put in it) with tokens
    /// from <paramref name="tokens"/>. Returns once the session is admitted and media is connected. Cancelling
    /// <paramref name="cancellationToken"/> abandons the attempt and leaves the room disconnected.
    /// </summary>
    public async Task ConnectAsync(string url, TokenProvider tokens, CancellationToken cancellationToken = default)
    {
        if (tokens is null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            throw SfuException.StateError("invalid_url", $"not a ws:// or wss:// URL: {url}");
        }

        long generation;
        CancellationToken lifetime;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_state != ConnectionState.Disconnected)
            {
                throw SfuException.StateError("invalid_state", $"cannot connect while {_state}");
            }

            _url = url;
            _tokens = tokens;
            _token = null;
            _tokenRejected = false;
            _policy.Reset();
            _connectGen++;
            _timers = new CancellationTokenSource();
            lifetime = _timers.Token;
            SetState(ConnectionState.Connecting);
            generation = _connectGen;
        }

        var completed = false;
        using var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
        try
        {
            var attempt = 0;
            while (true)
            {
                var reason = attempt == 0 ? TokenReason.Initial : TokenReason.Reconnect;
                Exception? failure = null;
                try
                {
                    await StartSessionAsync(generation, reason, connecting.Token).ConfigureAwait(false);
                    if (_options.AdmissionProbe is { } probe)
                    {
                        await probe().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    failure = e;
                }

                TimeSpan delay;
                CancellationToken abort;
                lock (_gate)
                {
                    if (generation != _connectGen)
                    {
                        throw SfuException.Disconnected("client");
                    }

                    var error = Admitted(failure);
                    if (error is null)
                    {
                        OnConnected();
                        completed = true;
                        return;
                    }

                    if (error.Kind == SfuErrorKind.Authentication)
                    {
                        Finish(DisconnectReason.AuthFailed, false);
                        completed = true;
                        throw error;
                    }

                    if (error.Kind == SfuErrorKind.State || _lastEnd?.Reason == "invalid_room")
                    {
                        Finish(DisconnectReason.JoinFailed, false);
                        completed = true;
                        throw error;
                    }

                    if (_lastEnd?.Reason == "room_full")
                    {
                        Finish(DisconnectReason.RoomFull, false);
                        completed = true;
                        throw error;
                    }

                    var decision = _policy.Next(_lastEnd?.Code, false, Clock.Now, Delay);
                    if (!decision.Reconnect || attempt >= 2)
                    {
                        Finish(ReasonFor(decision.Reconnect ? StopReason.Exhausted : decision.Stop), false);
                        completed = true;
                        throw error;
                    }

                    if (decision.RefreshToken)
                    {
                        _tokenRejected = true;
                    }

                    ResetSessionState();
                    delay = decision.Delay;
                    abort = _timers.Token;
                }

                attempt++;
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, abort);
                try
                {
                    await Task.Delay(Clock.Wait(delay), linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
            }
        }
        finally
        {
            if (!completed)
            {
                // Cancelled (or failed unexpectedly) while connecting: never leave a live session behind.
                lock (_gate)
                {
                    if (_connectGen == generation && _state == ConnectionState.Connecting)
                    {
                        _connectGen++;
                        Finish(DisconnectReason.Client, true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Leaves the room and releases every resource. Returns once the socket and the PeerConnection are closed.
    /// The room can connect again afterwards.
    /// </summary>
    public async Task DisconnectAsync()
    {
        List<Task> closing;
        lock (_gate)
        {
            _connectGen++;
            if (_state != ConnectionState.Disconnected || _session is not null)
            {
                Finish(DisconnectReason.Client, true);
            }

            closing = _closing.ToList();
            _closing.Clear();
        }

        foreach (var task in closing)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "closing a session failed");
            }
        }
    }

    /// <summary>
    /// Disconnects (see <see cref="DisconnectAsync"/>) and completes <see cref="Events"/>. The room cannot be used
    /// afterwards. Concurrent calls all return once the first one finished.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposing is null)
            {
                _disposed = true;
                _disposing = Task.Run(DisposeCoreAsync);
            }

            return new ValueTask(_disposing);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        CancellationTokenSource timers;
        lock (_gate)
        {
            timers = _timers;
            _events.Writer.TryComplete();
        }

        CancelTimers(timers);
    }

    /// <summary>Sends a text message to everyone else in the room (at most 2047 UTF-8 bytes).</summary>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        await RequestAsync(new ClientMessage.SendMessage(message), m => m is ServerMessage.MessageSent ? Ack.Ok : null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies a moderation token (issued by the Mezon API for a target user, metadata "mute" or "kick").</summary>
    public async Task<ModerationResult> ModerateAsync(string token, CancellationToken cancellationToken = default)
    {
        var ack = await RequestAsync(
            new ClientMessage.ParticipantAction(token),
            m => m is ServerMessage.ParticipantActionCompleted c ? new Ack.Moderation(c.Action, c.UserId, c.Affected) : null,
            cancellationToken).ConfigureAwait(false);
        return ack is Ack.Moderation moderation
            ? new ModerationResult(moderation.Action, moderation.UserId, moderation.Affected)
            : throw SfuException.Protocol("unexpected acknowledgement");
    }

    /// <summary>Mutes every session of <paramref name="userId"/> (needs <see cref="RoomOptions.ModerationTokens"/>). Returns the sessions affected.</summary>
    public Task<int> MuteParticipantAsync(string userId, CancellationToken cancellationToken = default) => ModerateUserAsync("mute", userId, cancellationToken);

    /// <summary>Disconnects every session of <paramref name="userId"/> (needs <see cref="RoomOptions.ModerationTokens"/>). Returns the sessions affected.</summary>
    public Task<int> KickParticipantAsync(string userId, CancellationToken cancellationToken = default) => ModerateUserAsync("kick", userId, cancellationToken);

    private async Task<int> ModerateUserAsync(string action, string userId, CancellationToken cancellationToken)
    {
        var provider = _options.ModerationTokens ?? throw SfuException.StateError("no_moderation_provider", "RoomOptions.ModerationTokens is not set");
        cancellationToken.ThrowIfCancellationRequested();
        string token;
        try
        {
            token = await provider.GetAsync(action, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            throw SfuException.Authentication("moderation_token_failed", e.Message, e);
        }

        return (await ModerateAsync(token, cancellationToken).ConfigureAwait(false)).Affected;
    }

    /// <summary>Asks the server to resend a keyframe of a participant's camera or screen.</summary>
    public async Task RequestKeyframeAsync(RemoteParticipant participant, TrackSource source, CancellationToken cancellationToken = default)
    {
        var publisher = participant.PeerId;
        await RequestAsync(
            new ClientMessage.RequestKeyframe(source == TrackSource.Screen ? VideoSource.Screen : VideoSource.Camera, publisher),
            m => m is ServerMessage.KeyframeRequested k && (k.PublisherId is null || k.PublisherId == publisher) ? Ack.Ok : null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops or resumes receiving remote camera and screen video. Survives reconnects.</summary>
    public async Task<VideoReceiving> SetVideoReceivingAsync(VideoReceiving receiving, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<Ack>? wait;
        lock (_gate)
        {
            ThrowIfDisposed();
            _visibility = receiving;
            wait = VisibilityRequest();
        }

        if (wait is null)
        {
            return receiving;
        }

        try
        {
            var ack = await WaitOrCancel(wait, cancellationToken).ConfigureAwait(false);
            return ack is Ack.Visibility v ? new VideoReceiving(v.Camera, v.Screen) : receiving;
        }
        catch (SfuException e) when (e.Kind is SfuErrorKind.Disconnected or SfuErrorKind.NotConnected)
        {
            return receiving;
        }
    }

    private async Task<Ack> RequestAsync(ClientMessage message, Func<ServerMessage, Ack?> matcher, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<Ack> wait;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_session is not { Phase: Phase.Active } state)
            {
                throw SfuException.NotConnected();
            }

            wait = RequestLocked(state, message, matcher);
        }

        return await WaitOrCancel(wait, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<T> WaitOrCancel<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            return await task.ConfigureAwait(false);
        }

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                _ = task.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
                throw new OperationCanceledException(cancellationToken);
            }
        }

        return await task.ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Room));
        }
    }

    // -----------------------------------------------------------------------------------------
    // Requests and local state (locked)

    private Task<Ack> RequestLocked(SessionState state, ClientMessage message, Func<ServerMessage, Ack?> matcher)
    {
        var (id, reply) = state.Correlator.Track(message.TypeName, matcher, Clock.DeadlineAfter(_options.Timeouts.Operation));
        try
        {
            state.Session.Send(message);
        }
        catch
        {
            state.Correlator.Remove(id);
            throw;
        }

        return reply;
    }

    private Task<Ack>? VisibilityRequest()
    {
        if (_session is not { Phase: Phase.Active } state)
        {
            return null;
        }

        return RequestLocked(
            state,
            new ClientMessage.Visibility(_visibility.Camera, _visibility.Screen, null),
            m => m is ServerMessage.VisibilityChanged v ? new Ack.Visibility(v.Camera, v.Screen) : null);
    }

    /// <summary>Waits for a request and ignores the end of the session (the wanted state is replayed after a reconnect).</summary>
    internal static async Task Settle(Task<Ack>? wait)
    {
        if (wait is null)
        {
            return;
        }

        try
        {
            await wait.ConfigureAwait(false);
        }
        catch (SfuException e) when (e.Kind is SfuErrorKind.Disconnected or SfuErrorKind.NotConnected)
        {
        }
    }

    private void SpawnSettle(Task<Ack>? wait, string what)
    {
        if (wait is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Settle(wait).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogWarning("{What} failed: {Error}", what, e.Message);
            }
        });
    }

    internal void ApplyUplinks()
    {
        if (_options.Role == Role.Audience)
        {
            _microphone.SetEnabled(_pushToTalk);
            _camera.SetEnabled(false);
        }
        else
        {
            _microphone.SetEnabled(_micEnabled);
            _camera.SetEnabled(_cameraEnabled);
        }
    }

    internal Task<Ack>? SendMute()
    {
        var muted = _options.Role == Role.Audience ? !_pushToTalk : !_micEnabled;
        if (_session is not { } state || !state.IsJoined)
        {
            return null;
        }

        var revision = state.Mute.Next(muted);
        try
        {
            return RequestLocked(
                state,
                new ClientMessage.Mute(muted, revision),
                m => m is ServerMessage.MuteChanged c && (c.MuteRevision is null || c.MuteRevision == revision) ? Ack.Ok : null);
        }
        catch (SfuException)
        {
            state.Mute.Reject();
            return null;
        }
    }

    internal Task<Ack>? CameraRequest()
    {
        var active = _cameraEnabled;
        var peer = _localPeerId;
        if (_session is not { Phase: Phase.Active } state)
        {
            return null;
        }

        try
        {
            return RequestLocked(state, new ClientMessage.Camera(active), m => m is ServerMessage.PeerUpdated p && p.State.PeerId == peer && p.State.CameraRequested == active ? Ack.Ok : null);
        }
        catch (SfuException)
        {
            return null;
        }
    }

    internal (Task<Ack>? Mute, Task<Ack>? PushToTalk) PushToTalkRequest()
    {
        if (_session is not { Phase: Phase.Active })
        {
            return (null, null);
        }

        var mute = SendMute();
        var active = _pushToTalk;
        Task<Ack>? ptt = null;
        if (_session is { } state)
        {
            try
            {
                ptt = RequestLocked(state, new ClientMessage.PushToTalk(active), m => m is ServerMessage.PushToTalkChanged c && c.Active == active ? Ack.Ok : null);
            }
            catch (SfuException)
            {
            }
        }

        return (mute, ptt);
    }

    internal Task SetMicrophoneEnabled(bool enabled)
    {
        Task<Ack>? wait;
        lock (_gate)
        {
            ThrowIfDisposed();
            _micEnabled = enabled;
            ApplyUplinks();
            wait = SendMute();
        }

        return Settle(wait);
    }

    internal Task SetCameraEnabled(bool enabled)
    {
        Task<Ack>? wait;
        lock (_gate)
        {
            ThrowIfDisposed();
            _cameraEnabled = enabled;
            ApplyUplinks();
            wait = CameraRequest();
        }

        return Settle(wait);
    }

    internal async Task SetPushToTalk(bool active)
    {
        Task<Ack>? mute;
        Task<Ack>? ptt;
        lock (_gate)
        {
            ThrowIfDisposed();
            _pushToTalk = active;
            ApplyUplinks();
            (mute, ptt) = PushToTalkRequest();
        }

        var settling = Settle(ptt);
        try
        {
            await Settle(mute).ConfigureAwait(false);
        }
        catch
        {
            _ = settling.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
            throw;
        }

        await settling.ConfigureAwait(false);
    }

    internal Task SetMetadata(string metadata)
    {
        Task<Ack>? wait = null;
        lock (_gate)
        {
            ThrowIfDisposed();
            _metadata = metadata;
            var peer = _localPeerId;

            // The acknowledgement echoes the text actually sent, after sanitizing.
            var wanted = Protocol.SanitizeText(metadata);
            if (_session is { } state && state.IsJoined)
            {
                wait = RequestLocked(state, new ClientMessage.PeerUpdate(wanted), m => m is ServerMessage.PeerUpdated p && p.State.PeerId == peer && p.State.Metadata == wanted ? Ack.Ok : null);
            }
        }

        return Settle(wait);
    }

    internal (bool Mic, bool Camera, bool PushToTalk, uint PeerId, string? UserId, ParticipantState? ServerState) LocalSnapshot()
    {
        lock (_gate)
        {
            return (_micEnabled, _cameraEnabled, _pushToTalk, _localPeerId, _localUserId, _serverState);
        }
    }

    /// <summary>Re-applies wanted state on a freshly admitted session (first connect or reconnect).</summary>
    private void FlushDesiredState()
    {
        ApplyUplinks();
        if (_options.Role == Role.Speaker)
        {
            if (_cameraEnabled)
            {
                SpawnSettle(CameraRequest(), "restoring the camera");
            }
        }
        else if (_pushToTalk)
        {
            var (mute, ptt) = PushToTalkRequest();
            SpawnSettle(mute, "restoring push-to-talk");
            SpawnSettle(ptt, "restoring push-to-talk");
        }
        else if (_session!.Mute.LastSent == false)
        {
            SpawnSettle(SendMute(), "muting after push-to-talk");
        }

        if (!_visibility.Camera || !_visibility.Screen)
        {
            try
            {
                SpawnSettle(VisibilityRequest(), "restoring video receiving");
            }
            catch (SfuException e)
            {
                _logger.LogWarning("restoring video receiving failed: {Error}", e.Message);
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // Sessions

    private enum Phase
    {
        Opening,
        Joining,
        Negotiating,
        Active,
        Closed,
    }

    private sealed record SessionEnd(int? Code, string Reason, bool ClientInitiated, SfuException? Error);

    /// <summary>The parts of a session shared with its tasks.</summary>
    private sealed class Session
    {
        private long _lastInbound = Clock.Now.Ticks;

        public long Id { get; } = Interlocked.Increment(ref _sessionIds);

        public Channel<string?> Out { get; } = Channel.CreateUnbounded<string?>(new UnboundedChannelOptions { SingleReader = true });

        public CancellationTokenSource Stop { get; } = new();

        public object OfferLock { get; } = new();

        public ServerMessage.Offer? Offer { get; set; }

        public SemaphoreSlim OfferReady { get; } = new(0, 1);

        public Task? Writer { get; set; }

        /// <summary>Remote mid → the track its RTP goes to (read by the WebRTC receive thread without the room lock).</summary>
        public ConcurrentDictionary<uint, RemoteTrack> Routes { get; } = new();

        public TimeSpan LastInbound
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref _lastInbound));
            set => Interlocked.Exchange(ref _lastInbound, value.Ticks);
        }

        public void Send(ClientMessage message)
        {
            string frame;
            try
            {
                frame = Protocol.Encode(message);
            }
            catch (EncodeException e)
            {
                throw SfuException.Protocol(e.Message, e);
            }

            if (!Out.Writer.TryWrite(frame))
            {
                throw SfuException.NotConnected();
            }
        }

        public void SignalOffer()
        {
            try
            {
                OfferReady.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        public void Close()
        {
            Out.Writer.TryWrite(null);
            Out.Writer.TryComplete();
        }
    }

    /// <summary>The mutable state of the current session, guarded by the room lock.</summary>
    private sealed class SessionState
    {
        public SessionState(Session session, TimeSpan joinDeadline, TaskCompletionSource<bool> started)
        {
            Session = session;
            JoinDeadline = joinDeadline;
            Started = started;
        }

        public Session Session { get; }

        public Phase Phase { get; set; } = Phase.Opening;

        public bool Joined { get; set; }

        public bool SnapshotSeen { get; set; }

        public IPeerConnection? Pc { get; set; }

        /// <summary>Creates the PeerConnection, or closes it when the session ended meanwhile.</summary>
        public Task? PcCreation { get; set; }

        public Correlator Correlator { get; } = new();

        public MuteSync Mute { get; } = new();

        public TimeSpan JoinDeadline { get; set; }

        public TimeSpan? AdmissionDeadline { get; set; }

        public TimeSpan? IceDisconnectedAt { get; set; }

        public bool PcConnected { get; set; }

        public HashSet<uint> Receiving { get; set; } = new();

        public TaskCompletionSource<bool>? Started { get; set; }

        public bool IsJoined => Joined && Phase != Phase.Closed;
    }

    private void Emit(RoomEvent e) => _events.Writer.TryWrite(e);

    private TimeSpan Delay(int attempt) =>
        _options.Reconnect.Delay is { } delay ? delay(attempt) : ReconnectPolicy.DefaultDelay(attempt, Rand.NextDouble());

    private void SetState(ConnectionState state)
    {
        if (_state != state)
        {
            _state = state;
            Emit(new ConnectionStateChanged(state));
        }
    }

    private async Task<string> ResolveTokenAsync(long generation, TokenReason reason, CancellationToken cancellationToken)
    {
        string? token;
        TokenProvider? provider;
        lock (_gate)
        {
            if (_tokenRejected)
            {
                reason = TokenReason.Rejected;
            }

            token = _token;
            provider = _tokens;
        }

        if (provider is null)
        {
            throw SfuException.Authentication("missing_token", "no token provider");
        }

        if (token is not null && reason != TokenReason.Rejected && MeetToken.NeedsRefresh(token, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60)))
        {
            token = null;
            reason = TokenReason.Expiring;
        }

        if (reason == TokenReason.Rejected)
        {
            if (provider.IsFixed)
            {
                throw SfuException.Authentication("token_rejected", "the server rejected the token and the provider is fixed");
            }

            token = null;
        }

        if (token is null)
        {
            string? previous;
            lock (_gate)
            {
                if (generation != _connectGen)
                {
                    throw SfuException.Disconnected("client");
                }

                previous = _token;
            }

            string fresh;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(Clock.Delay(_options.Timeouts.Token));
                Task<string> fetch;
                try
                {
                    fetch = provider.GetAsync(new TokenRequest(reason, previous), timeout.Token);
                }
                catch (Exception e)
                {
                    throw SfuException.Authentication("token_provider_failed", e.Message, e);
                }

                try
                {
                    fresh = await WaitOrCancel(fetch, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException e)
                {
                    throw SfuException.Authentication("token_provider_failed", "timed out", e);
                }
                catch (Exception e)
                {
                    throw SfuException.Authentication("token_provider_failed", e.Message, e);
                }
            }

            if (string.IsNullOrEmpty(fresh))
            {
                throw SfuException.Authentication("missing_token", "empty token");
            }

            token = fresh;
        }

        lock (_gate)
        {
            if (generation == _connectGen)
            {
                _token = token;
            }
        }

        return token;
    }

    private async Task<ClientWebSocket> DialAsync(string url, CancellationToken cancellationToken)
    {
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.Zero;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Clock.Delay(_options.Timeouts.Dial));
        try
        {
            await ws.ConnectAsync(new Uri(url), timeout.Token).ConfigureAwait(false);
            return ws;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ws.Dispose();
            throw;
        }
        catch (OperationCanceledException)
        {
            ws.Dispose();
            throw SfuException.Timeout("dial");
        }
        catch (Exception e) when (e is WebSocketException or IOException or System.Net.Http.HttpRequestException or InvalidOperationException or System.Security.Authentication.AuthenticationException)
        {
            ws.Dispose();
            throw SfuException.Socket(e.Message, e);
        }
    }

    private async Task StartSessionAsync(long generation, TokenReason reason, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = await ResolveTokenAsync(generation, reason, cancellationToken).ConfigureAwait(false);
        }
        catch (SfuException e)
        {
            lock (_gate)
            {
                if (generation == _connectGen)
                {
                    _lastEnd = new SessionEnd(null, "auth_failed", false, e);
                }
            }

            throw;
        }

        var session = new Session();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = started.Task.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        string url;
        lock (_gate)
        {
            if (generation != _connectGen)
            {
                throw SfuException.Disconnected("client");
            }

            _tokenRejected = false;
            if (MeetToken.Decode(token) is { } claims)
            {
                _localUserId = claims.UserId;
                _roomId = claims.RoomId;
            }

            _session = new SessionState(session, Clock.DeadlineAfter(_options.Timeouts.Join), started);
            _lastEnd = null;
            url = _url;
        }

        ClientWebSocket? ws = null;
        SfuException? dialError = null;
        try
        {
            ws = await DialAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (SfuException e)
        {
            dialError = e;
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (IsCurrent(session))
                {
                    EndSession(session, new SessionEnd(null, "client", true, null));
                }
            }

            throw;
        }

        lock (_gate)
        {
            if (!IsCurrent(session))
            {
                ws?.Abort();
                ws?.Dispose();
                throw SfuException.Disconnected("client");
            }

            if (dialError is not null || ws is null)
            {
                var error = dialError ?? SfuException.Socket("no socket");
                EndSession(session, new SessionEnd(CloseCodes.Abnormal, "socket_open_failed", false, error));
                throw error;
            }

            var reader = Task.Run(() => ReadLoopAsync(session, ws));
            session.Writer = Task.Run(() => WriteLoopAsync(session, ws, reader));
            _ = Task.Run(() => GuardAsync(session, "timers", () => TickLoopAsync(session)));
            var state = _session!;
            state.Phase = Phase.Joining;
            state.JoinDeadline = Clock.DeadlineAfter(_options.Timeouts.Join);
            try
            {
                session.Send(new ClientMessage.Join(_options.Role, _options.ScreenCodec, token));
            }
            catch (SfuException e)
            {
                EndSession(session, new SessionEnd(null, "join_send_failed", false, e));
            }
        }

        await WaitOrCancel(started.Task, cancellationToken).ConfigureAwait(false);
    }

    private bool IsActive() => _session is { Phase: Phase.Active };

    private SfuException? Admitted(Exception? failure)
    {
        if (failure is null)
        {
            return IsActive() ? null : SfuException.Disconnected(_lastEnd?.Reason ?? "session_ended");
        }

        return failure as SfuException ?? SfuException.WebRtc(failure.Message, failure);
    }

    private bool IsCurrent(Session session) => _session is { } s && s.Session.Id == session.Id && s.Phase != Phase.Closed;

    private static async Task WriteLoopAsync(Session session, ClientWebSocket ws, Task reader)
    {
        try
        {
            while (await session.Out.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (session.Out.Reader.TryRead(out var frame))
                {
                    if (frame is null)
                    {
                        using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        {
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "leave", closeTimeout.Token).ConfigureAwait(false);
                        }

                        return;
                    }

                    using var sendTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(frame)), WebSocketMessageType.Text, true, sendTimeout.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception)
        {
            // The reader reports the end of the socket.
        }
        finally
        {
            // Let the reader see the server's close reply, then release the socket.
            await Task.WhenAny(reader, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            ws.Abort();
            ws.Dispose();
        }
    }

    private async Task ReadLoopAsync(Session session, ClientWebSocket ws)
    {
        var code = CloseCodes.Abnormal;
        var reason = "socket_closed";
        var buffer = new byte[64 * 1024];
        try
        {
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (ws.CloseStatus is { } status && status != WebSocketCloseStatus.Empty)
                    {
                        code = (int)status;
                        reason = string.IsNullOrEmpty(ws.CloseStatusDescription) ? "socket_closed" : ws.CloseStatusDescription!;
                    }

                    break;
                }

                session.LastInbound = Clock.Now;
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                ServerMessage decoded;
                try
                {
                    decoded = Protocol.Decode(text);
                }
                catch (DecodeException e)
                {
                    _logger.LogWarning("dropping undecodable frame: {Error}", e.Message);
                    continue;
                }

                if (decoded is ServerMessage.Ping)
                {
                    try
                    {
                        session.Send(new ClientMessage.Pong());
                    }
                    catch (SfuException)
                    {
                    }

                    continue;
                }

                OnServerMessage(session, decoded);
            }
        }
        catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "signaling reader failed");
        }

        lock (_gate)
        {
            if (IsCurrent(session))
            {
                EndSession(session, new SessionEnd(code, reason, false, null));
            }
        }
    }

    /// <summary>Runs a task of <paramref name="session"/>: an unexpected exception is logged and ends the session.</summary>
    private async Task GuardAsync(Session session, string what, Func<Task> run)
    {
        try
        {
            await run().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            lock (_gate)
            {
                if (!IsCurrent(session))
                {
                    _logger.LogDebug(e, "the {What} of an ended session failed", what);
                    return;
                }

                _logger.LogError(e, "the session's {What} failed", what);
                EndSession(session, new SessionEnd(null, "internal_error", false, SfuException.Disconnected("internal_error", e)));
            }
        }
    }

    private async Task TickLoopAsync(Session session)
    {
        var stop = session.Stop.Token;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Tick, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_gate)
            {
                if (!IsCurrent(session))
                {
                    return;
                }

                OnTick(session);
            }
        }
    }

    private void OnTick(Session session)
    {
        var now = Clock.Now;
        var timeouts = _options.Timeouts;
        var state = _session!;
        foreach (var operation in state.Correlator.Expire(now))
        {
            if (operation == "mute")
            {
                state.Mute.Reject();
            }
        }

        string? expired = null;
        if (!state.Joined && now >= state.JoinDeadline)
        {
            expired = "join_timeout";
        }
        else if (state.Joined && state.Phase != Phase.Active && state.AdmissionDeadline is { } admission && now >= admission)
        {
            expired = "admission_timeout";
        }
        else if (now >= Clock.Deadline(session.LastInbound, timeouts.InboundIdle))
        {
            expired = "signaling_idle";
        }
        else if (state.IceDisconnectedAt is { } at && now - at >= TimeSpan.FromSeconds(5))
        {
            expired = "ice_disconnected";
        }

        if (expired is not null)
        {
            EndSession(session, new SessionEnd(null, expired, false, SfuException.Timeout(expired)));
            return;
        }

        if (state.Phase != Phase.Active)
        {
            return;
        }

        if (state.Mute.TakeForcedMute(now))
        {
            _micEnabled = false;
            ApplyUplinks();
            SpawnSettle(SendMute(), "muting after a forced mute");
            Emit(new LocalMuteForced());
        }

        SampleSpeakers(now);
    }

    /// <summary>Ends a session (locked) and reacts to it: reconnect, or finish the room.</summary>
    private void EndSession(Session session, SessionEnd end)
    {
        if (_session is not { } state || state.Session.Id != session.Id || state.Phase == Phase.Closed)
        {
            return;
        }

        state.Phase = Phase.Closed;
        session.Stop.Cancel();
        session.SignalOffer();
        var reason = end.ClientInitiated ? "client" : end.Reason;
        state.Correlator.RejectAll(SfuException.Disconnected(reason));
        session.Routes.Clear();
        session.Close();
        if (session.Writer is { } writer)
        {
            _closing.RemoveAll(t => t.IsCompleted);
            _closing.Add(writer);
        }

        if (state.Pc is { } pc)
        {
            state.Pc = null;
            _closing.RemoveAll(t => t.IsCompleted);
            _closing.Add(Task.Run(() => pc.CloseAsync()));
        }
        else if (state.PcCreation is { IsCompleted: false } creation)
        {
            _closing.RemoveAll(t => t.IsCompleted);
            _closing.Add(creation);
        }

        if (state.Started is { } started)
        {
            state.Started = null;
            var name = end.Code is { } code ? CloseCodes.NameOf(code) : null;
            started.TrySetException(end.Error ?? SfuException.Disconnected(name is null ? end.Reason : $"{end.Reason} ({name})"));
        }

        _microphone.Bind(null);
        _camera.Bind(null);
        OnSessionEnded(end);
    }

    private void OnSessionEnded(SessionEnd end)
    {
        _logger.LogInformation("session ended: {Reason} {Code} {Error}", end.Reason, end.Code, end.Error?.Message);
        _lastEnd = end;
        if (end.ClientInitiated || _state is ConnectionState.Disconnected or ConnectionState.Connecting)
        {
            return;
        }

        if (end.Reason == "room_full")
        {
            Finish(DisconnectReason.RoomFull, false);
            return;
        }

        if (end.Reason == "invalid_room")
        {
            Finish(DisconnectReason.JoinFailed, false);
            return;
        }

        var decision = _policy.Next(end.Code, false, Clock.Now, Delay);
        if (!decision.Reconnect)
        {
            Finish(ReasonFor(decision.Stop), false);
            return;
        }

        if (decision.RefreshToken)
        {
            _tokenRejected = true;
        }

        BeginReconnect(end.Reason, decision.Delay);
    }

    private void BeginReconnect(string reason, TimeSpan delay)
    {
        if (_state != ConnectionState.Reconnecting)
        {
            SetState(ConnectionState.Reconnecting);
            Emit(new Reconnecting(reason));
        }

        ResetSessionState();
        var generation = _connectGen;
        var stop = _timers.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Clock.Wait(delay), stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_gate)
            {
                if (_connectGen != generation)
                {
                    return;
                }
            }

            Exception? failure = null;
            try
            {
                await StartSessionAsync(generation, TokenReason.Reconnect, stop).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                failure = e;
            }

            lock (_gate)
            {
                if (generation != _connectGen)
                {
                    return;
                }

                switch (failure)
                {
                    case null:
                        if (IsActive())
                        {
                            OnConnected();
                        }

                        break;
                    case SfuException { Kind: SfuErrorKind.Authentication, Code: "token_provider_failed" }:
                        // A provider failure is retried under the reconnect policy (it ended no session).
                        if (_lastEnd is { } last)
                        {
                            OnSessionEnded(last);
                        }

                        break;
                    case SfuException { Kind: SfuErrorKind.Authentication }:
                        Finish(DisconnectReason.AuthFailed, false);
                        break;
                    default:
                        _logger.LogWarning("reconnect attempt failed: {Error}", failure.Message);
                        break;
                }
            }
        });
    }

    private void OnConnected()
    {
        var wasReconnecting = _state == ConnectionState.Reconnecting;
        SetState(ConnectionState.Connected);
        if (wasReconnecting)
        {
            Emit(new Reconnected());
        }

        var session = _session?.Session;
        var stop = _timers.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_options.StableAfter, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_gate)
            {
                if (session is not null && _session?.Session == session && IsActive())
                {
                    _policy.Reset();
                }
            }
        });
    }

    private DisconnectReason ReasonFor(StopReason stop)
    {
        if (_lastEnd is { } end)
        {
            if (end.Error?.Kind == SfuErrorKind.Authentication)
            {
                return DisconnectReason.AuthFailed;
            }

            if (end.Reason == "room_full")
            {
                return DisconnectReason.RoomFull;
            }
        }

        return stop switch
        {
            StopReason.Exhausted when _state == ConnectionState.Connecting => DisconnectReason.JoinFailed,
            StopReason.Client => DisconnectReason.Client,
            StopReason.Kicked => DisconnectReason.Kicked,
            StopReason.DuplicateSession => DisconnectReason.DuplicateSession,
            StopReason.AloneTimeout => DisconnectReason.AloneTimeout,
            StopReason.ServerClosed => DisconnectReason.ServerClosed,
            StopReason.ServerMisconfigured => DisconnectReason.ServerMisconfigured,
            StopReason.AuthFailed => DisconnectReason.AuthFailed,
            StopReason.ProtocolRejected => DisconnectReason.ProtocolRejected,
            _ => DisconnectReason.ReconnectExhausted,
        };
    }

    /// <summary>Leaves the room for good (locked).</summary>
    private void Finish(DisconnectReason reason, bool clientInitiated)
    {
        var timers = _timers;
        _ = Task.Run(() => CancelTimers(timers));
        _timers = new CancellationTokenSource();
        _connectGen++;
        _detector.Clear();
        if (_session is { } state)
        {
            EndSession(state.Session, new SessionEnd(null, clientInitiated ? "client" : reason.ToString(), true, null));
            _session = null;
        }

        UnsubscribeAll();
        foreach (var e in _membership.Clear())
        {
            ApplyMemberEvent(e);
        }

        _slots.Clear();
        _dynamic.Clear();
        _mode = DownlinkMode.Unknown;
        _slotCount = 0;
        _pools = null;
        _localPeerId = 0;
        _serverState = null;
        _micEnabled = false;
        _cameraEnabled = false;
        _pushToTalk = false;
        ClearSpeakers();
        ApplyUplinks();
        var wasConnected = _state != ConnectionState.Disconnected;
        SetState(ConnectionState.Disconnected);
        if (wasConnected)
        {
            Emit(new Disconnected(reason));
        }
    }

    /// <summary>Cancels the timers and pending attempts of a finished room, never under the room lock (continuations run inline).</summary>
    private void CancelTimers(CancellationTokenSource timers)
    {
        try
        {
            timers.Cancel();
        }
        catch (AggregateException e)
        {
            _logger.LogWarning(e, "a cancellation callback failed");
        }
    }

    /// <summary>Forgets the state of the session that ended, before the next attempt.</summary>
    private void ResetSessionState()
    {
        UnsubscribeAll();
        _membership.ResetSession();
        _slots.Clear();
        _dynamic.Clear();
        _mode = DownlinkMode.Unknown;
        _slotCount = 0;
        _pools = null;
        _localPeerId = 0;
        _serverState = null;
        ClearSpeakers();
    }

    private void ClearSpeakers()
    {
        _detector.Clear();
        foreach (var p in _participants.Values)
        {
            p.SetSpeaking(false, 0);
        }

        SetSpeakers(new List<RemoteParticipant>());
    }

    // -----------------------------------------------------------------------------------------
    // Server messages

    private void OnServerMessage(Session session, ServerMessage message)
    {
        lock (_gate)
        {
            if (!IsCurrent(session))
            {
                return;
            }

            var state = _session!;
            switch (message)
            {
                case ServerMessage.Joined joined:
                    OnJoined(session, joined);
                    return;
                case ServerMessage.Offer offer:
                    if (state.Phase == Phase.Joining)
                    {
                        state.Phase = Phase.Negotiating;
                    }

                    lock (session.OfferLock)
                    {
                        session.Offer = offer;
                    }

                    session.SignalOffer();
                    return;
                case ServerMessage.Error error:
                    if (state.Correlator.HandleError(error.Code) is { } operation)
                    {
                        if (operation == "mute")
                        {
                            state.Mute.Reject();
                        }
                    }
                    else
                    {
                        OnUnclaimedError(session, error.Code);
                    }

                    return;
            }

            state.Correlator.HandleMessage(message);
            var now = Clock.Now;
            switch (message)
            {
                case ServerMessage.RoomSnapshot snapshot:
                    state.SnapshotSeen = true;
                    ApplyMemberEvents(_membership.ApplySnapshot(snapshot, now));
                    AssignSnapshotSlots(snapshot);
                    Reconcile();
                    CheckActive();
                    break;
                case ServerMessage.PeerJoined joined:
                    ApplyMemberEvents(_membership.ApplyJoined(joined, now));
                    Reconcile();
                    break;
                case ServerMessage.PeerLeft left:
                    ApplyMemberEvents(_membership.ApplyLeft(left, now));
                    Reconcile();
                    break;
                case ServerMessage.PeerUpdated updated:
                    ApplyMemberEvents(_membership.ApplyUpdated(updated.State, now));
                    Reconcile();
                    break;
                case ServerMessage.SlotAssigned m:
                    SetMode(DownlinkMode.Fixed);
                    _slots.Assign(m.Slot, new SlotOccupant(m.PeerId, m.UserId, m.Generation, m.AudioActive, m.VideoActive, m.ScreenActive));
                    _membership.SetMapping(m.PeerId, new SlotMapping(m.MidAudio, m.MidVideo, m.MidScreen, m.Slot, m.Generation));
                    Reconcile();
                    break;
                case ServerMessage.SlotReleased released:
                    SetMode(DownlinkMode.Fixed);
                    _slots.Release(released.Slot, released.Generation);
                    Reconcile();
                    break;
                case ServerMessage.MediaSlotAssigned m:
                    _pools?.Assign(m.Kind, m.Slot, m.Mid, m.PeerId, m.UserId, m.Generation, m.Active);
                    Reconcile();
                    break;
                case ServerMessage.MediaSlotReleased released:
                    _pools?.Release(released.Kind, released.Slot, released.Generation);
                    Reconcile();
                    break;
                case ServerMessage.MuteChanged changed:
                    state.Mute.Acknowledge(changed.IsMute, changed.MuteRevision);
                    break;
                case ServerMessage.RoomMessage roomMessage:
                    _participants.TryGetValue(roomMessage.PeerId, out var sender);
                    Emit(new MessageReceived(roomMessage.Message, roomMessage.UserId, roomMessage.PeerId, sender));
                    break;
                case ServerMessage.Unknown unknown:
                    _logger.LogDebug("ignoring unknown message {Type}", unknown.RawType);
                    break;
            }
        }
    }

    private void OnUnclaimedError(Session session, string code)
    {
        switch (code)
        {
            case "room_capacity":
                EndSession(session, new SessionEnd(null, "room_full", false, null));
                return;
            case "stale_offer_generation":
            case "future_offer_generation":
                _logger.LogDebug("server dropped an answer: {Code}", code);
                return;
            case "stale_mute_revision":
                _session?.Mute.Reject();
                break;
        }

        var info = ServerErrors.Find(code);
        Emit(new SignalError(SfuException.Signaling(code, null, info?.Retryable ?? false)));
        if (info is { Fatal: true } && info.Sources.Contains("answer"))
        {
            EndSession(session, new SessionEnd(null, code, false, SfuException.Signaling(code, "answer", info.Retryable)));
        }
        else if (code is "invalid_room" or "room_creation_failed")
        {
            EndSession(session, new SessionEnd(null, code, false, SfuException.Signaling(code, "join", info?.Retryable ?? false)));
        }
    }

    private List<IceServer> IceServersFor(IReadOnlyList<WireIceServer> fromServer)
    {
        var policy = _options.EffectiveIcePolicy;
        var servers = new List<IceServer>();
        if (policy != IceServersPolicy.Override)
        {
            servers.AddRange(fromServer.Select(s => new IceServer(s.Urls, s.Username, s.Credential)));
        }

        if (policy != IceServersPolicy.FromServer)
        {
            servers.AddRange(_options.IceServers);
        }

        return servers;
    }

    private void OnJoined(Session session, ServerMessage.Joined joined)
    {
        var state = _session!;
        if (state.Joined)
        {
            return;
        }

        state.Joined = true;
        state.AdmissionDeadline = Clock.DeadlineAfter(_options.Timeouts.Admission);
        _localPeerId = joined.PeerId;
        _membership.SetSelfPeerId(joined.PeerId);
        if (joined.Room.Length > 0)
        {
            _roomId = joined.Room;
        }

        if (joined.MediaPool is { } pool)
        {
            _pools = new MediaPools(pool);
            SetMode(DownlinkMode.Decoupled);
        }

        var config = new PeerConfig
        {
            IceServers = IceServersFor(joined.IceServers),
            BindAddress = _options.BindAddress,
            Camera = _options.Role == Role.Speaker,
            Logger = _logger,
            OnState = s => OnConnectionState(session, s),
            OnRtp = (mid, packet, mime) =>
            {
                if (session.Routes.TryGetValue(mid, out var track))
                {
                    track.Deliver(packet, mime);
                }
            },
        };

        // Sessions start muted on the server: state the microphone intent before the first answer.
        SpawnSettle(SendMute(), "initial mute");
        if (_metadata is { } metadata)
        {
            try
            {
                session.Send(new ClientMessage.PeerUpdate(metadata));
            }
            catch (SfuException e)
            {
                _logger.LogWarning("replaying metadata failed: {Error}", e.Message);
            }
        }

        var factory = _options.PeerFactory ?? SipsorceryPeer.CreateAsync;
        var stop = session.Stop.Token;
        var creation = Task.Run<IPeerConnection?>(async () =>
        {
            IPeerConnection? pc = null;
            Exception? failure = null;
            try
            {
                pc = await factory(config, stop).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                failure = e;
            }

            var current = false;
            lock (_gate)
            {
                if (pc is not null && IsCurrent(session))
                {
                    _session!.Pc = pc;
                    current = true;
                }
                else if (failure is not null && IsCurrent(session))
                {
                    EndSession(session, new SessionEnd(null, "peer_connection_failed", false, SfuException.WebRtc(failure.Message, failure)));
                }
            }

            if (pc is not null && !current)
            {
                await pc.CloseAsync().ConfigureAwait(false);
                return null;
            }

            return pc;
        });
        state.PcCreation = creation;
        _ = Task.Run(() => GuardAsync(session, "negotiation", async () =>
        {
            if (await creation.ConfigureAwait(false) is { } pc)
            {
                await NegotiateLoopAsync(session, pc).ConfigureAwait(false);
            }
        }));
    }

    /// <summary>
    /// Answers server offers one at a time. The server is always the offerer; every answer echoes the generation
    /// of the offer it answers. Only the newest pending offer is kept.
    /// </summary>
    private async Task NegotiateLoopAsync(Session session, IPeerConnection pc)
    {
        var stop = session.Stop.Token;
        long lastApplied = -1;
        var failures = 0;
        string? ufrag = null;
        var first = true;
        while (!stop.IsCancellationRequested)
        {
            ServerMessage.Offer? offer;
            lock (session.OfferLock)
            {
                offer = session.Offer;
                session.Offer = null;
            }

            if (offer is null)
            {
                try
                {
                    await session.OfferReady.WaitAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            if (offer.OfferGeneration < 0 || offer.OfferGeneration <= lastApplied)
            {
                continue;
            }

            if (!first)
            {
                try
                {
                    await Task.Delay(20, stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            first = false;
            var generation = offer.OfferGeneration;
            var layout = Sdp.AnalyzeOffer(offer.Sdp);
            var appliedRemote = false;
            string? failureCode = null;
            string? failureMessage = null;
            try
            {
                await pc.SetRemoteOfferAsync(offer.Sdp).ConfigureAwait(false);
                appliedRemote = true;
                var answer = await pc.CreateAnswerAsync().ConfigureAwait(false);
                var answerUfrag = Sdp.Parse(answer).IceUfrag;
                ufrag ??= answerUfrag;
                if (answerUfrag != ufrag)
                {
                    // The server pins the first ufrag; an ICE restart cannot be negotiated.
                    failureCode = "ice_ufrag_changed";
                    failureMessage = "local ICE ufrag changed";
                }
                else
                {
                    await pc.SetLocalAnswerAsync(answer).ConfigureAwait(false);
                    appliedRemote = false;
                }
            }
            catch (Exception e)
            {
                failureCode = appliedRemote ? "create_answer_failed" : "set_remote_failed";
                failureMessage = e.Message;
            }

            if (stop.IsCancellationRequested)
            {
                return;
            }

            if (failureCode is not null)
            {
                _logger.LogWarning("negotiation failed for offer {Generation}: {Error}", generation, failureMessage);
                if (appliedRemote)
                {
                    try
                    {
                        await pc.RollbackAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug(e, "rollback failed");
                    }
                }

                failures++;
                if (failureCode == "ice_ufrag_changed" || failures >= 3)
                {
                    lock (_gate)
                    {
                        if (IsCurrent(session))
                        {
                            EndSession(session, new SessionEnd(null, "negotiation_failed", false, SfuException.Negotiation(failureCode, failureMessage ?? failureCode)));
                        }
                    }

                    return;
                }

                continue;
            }

            failures = 0;
            lastApplied = generation;
            bool superseded;
            lock (session.OfferLock)
            {
                superseded = session.Offer is { } next && next.OfferGeneration > generation;
            }

            if (!superseded)
            {
                try
                {
                    session.Send(new ClientMessage.Answer(generation, pc.LocalSdp));
                }
                catch (SfuException e)
                {
                    _logger.LogWarning("could not send answer {Generation}: {Error}", generation, e.Message);
                }
            }

            var receiving = pc.ReceivingMids();
            var (microphone, camera) = pc.Uplinks();
            lock (_gate)
            {
                if (!IsCurrent(session))
                {
                    return;
                }

                OnNegotiated(layout, generation, receiving, microphone, camera);
            }
        }
    }

    private void OnNegotiated(OfferLayout layout, long generation, IReadOnlyCollection<uint> receiving, IAudioUplink? microphone, IVideoUplink? camera)
    {
        _microphone.Bind(microphone);
        _camera.Bind(camera);
        _session!.Receiving = new HashSet<uint>(receiving);
        _slotCount = layout.SlotCount;
        if (layout.Mode != DownlinkMode.Unknown)
        {
            SetMode(layout.Mode);
        }

        if (_mode is not (DownlinkMode.Fixed or DownlinkMode.Decoupled))
        {
            _dynamic.ApplyOffer(layout, generation, Clock.Now);
        }

        Reconcile();
    }

    private void OnConnectionState(Session session, PeerState state)
    {
        lock (_gate)
        {
            if (!IsCurrent(session))
            {
                return;
            }

            var current = _session!;
            switch (state)
            {
                case PeerState.Failed:
                    EndSession(session, new SessionEnd(null, "ice_failed", false, SfuException.Negotiation("ice_failed", "ICE failed")));
                    return;
                case PeerState.Closed:
                    EndSession(session, new SessionEnd(null, "peer_closed", false, SfuException.Negotiation("peer_closed", "the PeerConnection closed")));
                    return;
                case PeerState.Disconnected:
                    current.IceDisconnectedAt ??= Clock.Now;
                    break;
                default:
                    current.IceDisconnectedAt = null;
                    break;
            }

            if (state == PeerState.Connected)
            {
                current.PcConnected = true;
            }

            CheckActive();
        }
    }

    private void CheckActive()
    {
        var state = _session!;
        if (state.Phase is Phase.Active or Phase.Closed || !state.SnapshotSeen || !state.PcConnected)
        {
            return;
        }

        state.Phase = Phase.Active;
        if (state.Started is { } started)
        {
            state.Started = null;
            started.TrySetResult(true);
        }

        FlushDesiredState();
    }

    private void SetMode(DownlinkMode mode)
    {
        if (_mode == mode || mode == DownlinkMode.Unknown || _mode is DownlinkMode.Fixed or DownlinkMode.Decoupled)
        {
            return;
        }

        _mode = mode;
        Emit(new DownlinkModeDetected(mode));
    }

    // -----------------------------------------------------------------------------------------
    // Membership

    /// <summary>
    /// Seeds the slot map (or the decoupled pools) from every member's mapping in a snapshot, known members included:
    /// a new session learns the current slot holders only from its snapshot (<c>slot_assigned</c> is sent for
    /// changes only).
    /// </summary>
    private void AssignSnapshotSlots(ServerMessage.RoomSnapshot snapshot)
    {
        foreach (var member in snapshot.Members)
        {
            if (member.State.PeerId == snapshot.SelfPeerId)
            {
                continue;
            }

            if (Tracks.OccupantFromMapping(member.State.PeerId, member.State.UserId, member.Mapping, member.State) is { } assignment)
            {
                _slots.Assign(assignment.Slot, assignment.Occupant);
            }

            _pools?.Seed(member.State.PeerId, member.State.UserId, member.Mapping.MediaSlots);
        }
    }

    private void ApplyMemberEvents(List<MemberEvent> events)
    {
        foreach (var e in events)
        {
            ApplyMemberEvent(e);
        }
    }

    private void ApplyMemberEvent(MemberEvent e)
    {
        switch (e)
        {
            case MemberEvent.Joined joined:
            {
                var p = new RemoteParticipant(joined.Member.State);
                _participants[p.PeerId] = p;
                _dynamic.ForgetDeparture(p.PeerId);
                if (Tracks.OccupantFromMapping(p.PeerId, p.UserId, joined.Member.Mapping, joined.Member.State) is { } assignment)
                {
                    _slots.Assign(assignment.Slot, assignment.Occupant);
                }

                _pools?.Seed(p.PeerId, p.UserId, joined.Member.Mapping?.MediaSlots ?? default);
                Emit(new ParticipantConnected(p));
                break;
            }

            case MemberEvent.Left left:
            {
                var id = left.Member.State.PeerId;
                _slots.ReleasePeer(id);
                if (left.Mapping is { Slot: { } slot, AssignmentGeneration: > 0 } mapping)
                {
                    _slots.Release(slot, mapping.AssignmentGeneration);
                }

                _pools?.ReleasePeer(id, left.Mapping?.MediaSlots ?? default);
                _dynamic.RemovePeer(id, Clock.Now, TimeSpan.FromSeconds(30));
                if (!_participants.TryGetValue(id, out var p))
                {
                    return;
                }

                foreach (var mid in _published.Where(s => s.Value.PeerId == id).Select(s => s.Key).ToList())
                {
                    Unsubscribe(mid);
                }

                _participants.Remove(id);
                Emit(new ParticipantDisconnected(p));
                break;
            }

            case MemberEvent.Updated updated:
                if (_participants.TryGetValue(updated.Member.State.PeerId, out var participant) && participant.Update(updated.Member.State))
                {
                    Emit(new ParticipantUpdated(participant));
                }

                break;
            case MemberEvent.Own own:
                if (_options.Role == Role.Speaker)
                {
                    _session?.Mute.ObserveServerState(own.State.IsMuted, !_micEnabled, Clock.Now);
                }

                _serverState = own.State;
                Emit(new LocalParticipantUpdated(own.State));
                break;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Remote tracks

    private void Reconcile()
    {
        if (_session is not { Pc: not null } state)
        {
            return;
        }

        var receiving = state.Receiving;
        var desired = Tracks.Desired(_mode, _slotCount, _slots, _dynamic, _pools);
        foreach (var entry in desired.ToList())
        {
            var spec = entry.Value;
            if (!_participants.TryGetValue(spec.PeerId, out var p))
            {
                desired.Remove(entry.Key);
                continue;
            }

            // Dynamic and decoupled offers keep camera and screen m-lines sendonly even while the publisher sends
            // nothing, so video is subscribed only while the server reports it active.
            var inactiveVideo = _mode != DownlinkMode.Fixed
                                && ((spec.Source == TrackSource.Camera && !p.CameraActive) || (spec.Source == TrackSource.Screen && !p.ScreenActive));
            if (!receiving.Contains(entry.Key) || inactiveVideo)
            {
                desired.Remove(entry.Key);
            }
        }

        var diff = Tracks.Compare(_published, desired);
        foreach (var spec in diff.Unsubscribed)
        {
            Unsubscribe(spec.Mid);
        }

        foreach (var spec in diff.Subscribed)
        {
            if (!_participants.TryGetValue(spec.PeerId, out var p))
            {
                continue;
            }

            var track = new RemoteTrack(spec);
            state.Session.Routes[spec.Mid] = track;
            _published[spec.Mid] = spec;
            _publishedTracks[spec.Mid] = track;
            p.SetTrack(track);
            Emit(new TrackSubscribed(track, p));
        }

        foreach (var spec in diff.MuteChanged)
        {
            _published[spec.Mid] = spec;
            if (!_publishedTracks.TryGetValue(spec.Mid, out var track) || !_participants.TryGetValue(spec.PeerId, out var p))
            {
                continue;
            }

            track.SetMuted(spec.Muted);
            Emit(spec.Muted ? new TrackMuted(track, p) : new TrackUnmuted(track, p));
        }

        UpdateActiveSpeakers();
    }

    private void Unsubscribe(uint mid)
    {
        if (!_published.TryGetValue(mid, out var spec) || !_publishedTracks.TryGetValue(mid, out var track))
        {
            return;
        }

        _published.Remove(mid);
        _publishedTracks.Remove(mid);
        if (_session is { } current && current.Session.Routes.TryGetValue(mid, out var routed) && ReferenceEquals(routed, track))
        {
            current.Session.Routes.TryRemove(mid, out _);
        }

        track.End();
        if (_participants.TryGetValue(spec.PeerId, out var p))
        {
            p.RemoveTrack(track);
            Emit(new TrackUnsubscribed(track, p));
        }
    }

    private void UnsubscribeAll()
    {
        foreach (var mid in _published.Keys.ToList())
        {
            Unsubscribe(mid);
        }

        UpdateActiveSpeakers();
    }

    /// <summary>
    /// Fixed-slot and decoupled rooms: the server's voice activity detection decides who holds a slot (an audio slot
    /// in decoupled rooms), so the speakers are the occupied slots with active audio. Dynamic rooms: see
    /// <see cref="SampleSpeakers"/>.
    /// </summary>
    private void UpdateActiveSpeakers()
    {
        var next = new List<RemoteParticipant>();
        if (_mode is DownlinkMode.Fixed or DownlinkMode.Decoupled)
        {
            var held = _pools is { } pools ? pools[TrackSource.Microphone].Occupied : _slots.Occupied.Where(entry => entry.Key < _slotCount);
            foreach (var entry in held)
            {
                if (entry.Value.AudioActive && _participants.TryGetValue(entry.Value.PeerId, out var p) && !next.Contains(p))
                {
                    next.Add(p);
                }
            }

            foreach (var p in _participants.Values)
            {
                p.SetSpeaking(next.Contains(p), p.AudioLevel);
            }
        }
        else
        {
            next = _speakers.Where(p => _participants.TryGetValue(p.PeerId, out var current) && ReferenceEquals(current, p) && p.AudioTrack is not null).ToList();
        }

        SetSpeakers(next);
    }

    private void SampleSpeakers(TimeSpan now)
    {
        if (_mode is DownlinkMode.Fixed or DownlinkMode.Decoupled)
        {
            return;
        }

        var levels = new Dictionary<uint, double>();
        foreach (var entry in _participants)
        {
            var track = entry.Value.AudioTrack;
            levels[entry.Key] = track is not null && !track.IsMuted && !entry.Value.IsMuted ? track.SampleLevel(now) : 0;
        }

        var speaking = _detector.Update(levels, now);
        foreach (var entry in _participants)
        {
            entry.Value.SetSpeaking(_detector.IsSpeaking(entry.Key), levels[entry.Key]);
        }

        SetSpeakers(speaking.Where(_participants.ContainsKey).Select(id => _participants[id]).ToList());
    }

    private void SetSpeakers(List<RemoteParticipant> next)
    {
        if (next.Count == _speakers.Count && next.Zip(_speakers, ReferenceEquals).All(same => same))
        {
            return;
        }

        _speakers = next;
        Emit(new ActiveSpeakersChanged(next.ToList()));
    }
}
