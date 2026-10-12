using System.Collections.Generic;
using System.Linq;
using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>
/// One session of another user in the room. A user who joins from two devices, or who reconnects, appears as
/// separate participants (different peer ids). Thread-safe; instances are compared by reference.
/// </summary>
public sealed class RemoteParticipant
{
    private readonly object _lock = new();
    private readonly SortedDictionary<TrackSource, RemoteTrack> _tracks = new();
    private ParticipantState _state;
    private ParticipantInfo _info;
    private bool _speaking;
    private double _level;

    internal RemoteParticipant(ParticipantState state)
    {
        PeerId = state.PeerId;
        UserId = state.UserId;
        _state = state;
        _info = ParticipantMetadata.Parse(state.Metadata);
    }

    /// <summary>The participant's SFU session id.</summary>
    public uint PeerId { get; }

    /// <summary>The participant's Mezon user id (decimal int64).</summary>
    public string UserId { get; }

    /// <summary>Display name and avatar parsed from the metadata.</summary>
    public ParticipantInfo Info
    {
        get
        {
            lock (_lock)
            {
                return _info;
            }
        }
    }

    /// <summary>The latest server state.</summary>
    public ParticipantState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public Role Role => State.Role;

    public bool IsMuted => State.IsMuted;

    public bool CameraActive => State.CameraActive;

    public bool ScreenActive => State.ScreenActive;

    /// <summary>Whether the participant is an active speaker.</summary>
    public bool IsSpeaking
    {
        get
        {
            lock (_lock)
            {
                return _speaking;
            }
        }
    }

    /// <summary>Estimated audio level (0–1) in dynamic rooms.</summary>
    public double AudioLevel
    {
        get
        {
            lock (_lock)
            {
                return _level;
            }
        }
    }

    /// <summary>The subscribed track of <paramref name="source"/>, or null.</summary>
    public RemoteTrack? GetTrack(TrackSource source)
    {
        lock (_lock)
        {
            return _tracks.TryGetValue(source, out var track) ? track : null;
        }
    }

    public RemoteTrack? AudioTrack => GetTrack(TrackSource.Microphone);

    public RemoteTrack? CameraTrack => GetTrack(TrackSource.Camera);

    public RemoteTrack? ScreenTrack => GetTrack(TrackSource.Screen);

    /// <summary>Every subscribed track.</summary>
    public IReadOnlyList<RemoteTrack> Tracks
    {
        get
        {
            lock (_lock)
            {
                return _tracks.Values.ToList();
            }
        }
    }

    public override string ToString() => $"RemoteParticipant(peer {PeerId}, user {UserId})";

    internal bool Update(ParticipantState state)
    {
        lock (_lock)
        {
            if (_state == state)
            {
                return false;
            }

            if (_state.Metadata != state.Metadata)
            {
                _info = ParticipantMetadata.Parse(state.Metadata);
            }

            _state = state;
            return true;
        }
    }

    internal void SetTrack(RemoteTrack track)
    {
        lock (_lock)
        {
            _tracks[track.Source] = track;
        }
    }

    internal void RemoveTrack(RemoteTrack track)
    {
        lock (_lock)
        {
            if (_tracks.TryGetValue(track.Source, out var current) && ReferenceEquals(current, track))
            {
                _tracks.Remove(track.Source);
            }
        }
    }

    internal void SetSpeaking(bool speaking, double level)
    {
        lock (_lock)
        {
            _speaking = speaking;
            _level = level;
        }
    }
}
