using System.Collections.Generic;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>
/// Something that happened in a <see cref="Room"/>. Events arrive in order on <see cref="Room.Events"/>; read them
/// with <c>await foreach (var e in room.Events.ReadAllAsync())</c> and pattern-match the record types.
/// </summary>
public abstract record RoomEvent;

public sealed record ConnectionStateChanged(ConnectionState State) : RoomEvent;

/// <summary>The session dropped and the room is rejoining.</summary>
public sealed record Reconnecting(string Reason) : RoomEvent;

public sealed record Reconnected : RoomEvent;

/// <summary>The room left for good.</summary>
public sealed record Disconnected(DisconnectReason Reason) : RoomEvent;

public sealed record ParticipantConnected(RemoteParticipant Participant) : RoomEvent;

public sealed record ParticipantDisconnected(RemoteParticipant Participant) : RoomEvent;

/// <summary>Server-side state changed (metadata, role, mute, camera, screen).</summary>
public sealed record ParticipantUpdated(RemoteParticipant Participant) : RoomEvent;

/// <summary>A remote track is available; read it with <see cref="RemoteTrack.ReadRtpAsync"/>.</summary>
public sealed record TrackSubscribed(RemoteTrack Track, RemoteParticipant Participant) : RoomEvent;

/// <summary>A remote track ended; <see cref="RemoteTrack.ReadRtpAsync"/> returns null once drained.</summary>
public sealed record TrackUnsubscribed(RemoteTrack Track, RemoteParticipant Participant) : RoomEvent;

public sealed record TrackMuted(RemoteTrack Track, RemoteParticipant Participant) : RoomEvent;

public sealed record TrackUnmuted(RemoteTrack Track, RemoteParticipant Participant) : RoomEvent;

/// <summary>The participants currently speaking, loudest first.</summary>
public sealed record ActiveSpeakersChanged(IReadOnlyList<RemoteParticipant> Speakers) : RoomEvent;

/// <summary>A room message; <see cref="Participant"/> is null when the sender is not (yet) known.</summary>
public sealed record MessageReceived(string Message, string UserId, uint PeerId, RemoteParticipant? Participant) : RoomEvent;

/// <summary>A moderator muted this client; the microphone was disabled.</summary>
public sealed record LocalMuteForced : RoomEvent;

/// <summary>The local participant's server-confirmed state changed.</summary>
public sealed record LocalParticipantUpdated(ParticipantState State) : RoomEvent;

public sealed record DownlinkModeDetected(DownlinkMode Mode) : RoomEvent;

/// <summary>A server error that no pending request claimed.</summary>
public sealed record SignalError(SfuException Error) : RoomEvent;
