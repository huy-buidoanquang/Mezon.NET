namespace Mezon.Net.Sdk.Sfu;

/// <summary>Role of a participant. The server takes it from the join message, not from the token.</summary>
public enum Role
{
    /// <summary>Publishes microphone and camera.</summary>
    Speaker,

    /// <summary>Listens; talks only while push-to-talk is held.</summary>
    Audience,
}

/// <summary>Codec offered for this client's screen-share uplink.</summary>
public enum ScreenCodec
{
    Vp8,
    Vp9,
}

/// <summary>A participant's media stream.</summary>
public enum TrackSource
{
    Microphone,
    Camera,
    Screen,
}

/// <summary>How the server maps remote publishers to m-lines.</summary>
public enum DownlinkMode
{
    /// <summary>Not known yet (no remote m-line carries a publisher).</summary>
    Unknown,

    /// <summary>One slot per publisher, renegotiated as people come and go; msids name the publisher.</summary>
    Dynamic,

    /// <summary>A pool of slots reassigned to active speakers with slot events; msids name the slot.</summary>
    Fixed,
}

/// <summary>Connection state of a <see cref="Room"/>.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}

/// <summary>Why a room ended up disconnected.</summary>
public enum DisconnectReason
{
    /// <summary>The application called <see cref="Room.DisconnectAsync"/> or disposed the room.</summary>
    Client,

    /// <summary>A moderator kicked this user (close 4006).</summary>
    Kicked,

    /// <summary>The same user joined from another connection, in any room (close 4012).</summary>
    DuplicateSession,

    /// <summary>The server closed the room because this client stayed alone (close 4011).</summary>
    AloneTimeout,

    /// <summary>The server closed the session normally (close 1000).</summary>
    ServerClosed,

    /// <summary>The server has no JWT secret (close 4003).</summary>
    ServerMisconfigured,

    /// <summary>No usable meet token could be obtained, or the server rejected it twice.</summary>
    AuthFailed,

    /// <summary>The room reached its capacity (error <c>room_capacity</c>).</summary>
    RoomFull,

    /// <summary>The first connect failed, or the server rejected the token's room (error <c>invalid_room</c>).</summary>
    JoinFailed,

    /// <summary>The server rejected the protocol twice (close 1002/1008).</summary>
    ProtocolRejected,

    /// <summary>Reconnecting is disabled or its attempts/duration are exhausted.</summary>
    ReconnectExhausted,
}

/// <summary>Server-side state of a participant session, shared by snapshots and updates.</summary>
/// <param name="PeerId">The SFU session id (unique per connection).</param>
/// <param name="UserId">The Mezon user id, a decimal int64.</param>
/// <param name="Metadata">Free text from the token or a metadata update, conventionally <c>"display_name;avatar_url"</c>.</param>
public sealed record ParticipantState(
    uint PeerId,
    string UserId,
    string Metadata,
    Role Role,
    bool IsMuted,
    bool CameraRequested,
    bool CameraActive,
    bool ScreenRequested,
    bool ScreenActive)
{
    internal static ParticipantState Empty(uint peerId) => new(peerId, "0", "", Role.Speaker, false, false, false, false, false);
}

/// <summary>Which remote video this client receives.</summary>
public readonly record struct VideoReceiving(bool Camera, bool Screen);

/// <summary>The outcome of a moderation token.</summary>
/// <param name="Affected">Sessions of the user that were muted or kicked.</param>
public sealed record ModerationResult(string Action, string UserId, int Affected);
