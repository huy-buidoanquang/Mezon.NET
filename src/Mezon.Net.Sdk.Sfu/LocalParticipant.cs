using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>
/// This client in the room. The wanted state (microphone, camera, push-to-talk, metadata) can be set before
/// connecting and is replayed after every reconnect. When the room ends (<see cref="Disconnected"/>), the
/// microphone, camera and push-to-talk go back to off; metadata and video receiving are kept. Thread-safe.
/// </summary>
public sealed class LocalParticipant
{
    private readonly Room _room;

    internal LocalParticipant(Room room) => _room = room;

    /// <summary>The microphone uplink: write Opus frames to it.</summary>
    public LocalAudioTrack Microphone => _room.Microphone;

    /// <summary>The camera uplink (speakers): write VP8 frames to it.</summary>
    public LocalVideoTrack Camera => _room.Camera;

    public Role Role => _room.Options.Role;

    /// <summary>This client's SFU session id, 0 while not joined.</summary>
    public uint PeerId => _room.LocalSnapshot().PeerId;

    /// <summary>The user id from the meet token.</summary>
    public string? UserId => _room.LocalSnapshot().UserId;

    public bool IsMicrophoneEnabled => _room.LocalSnapshot().Mic;

    public bool IsCameraEnabled => _room.LocalSnapshot().Camera;

    public bool IsPushToTalkActive => _room.LocalSnapshot().PushToTalk;

    /// <summary>The latest state the server reported for this client.</summary>
    public ParticipantState? ServerState => _room.LocalSnapshot().ServerState;

    /// <summary>
    /// Publishes or mutes the microphone (speakers). Sessions start muted on the server, so a speaker that wants to
    /// be heard must enable it. Can be called before connecting.
    /// </summary>
    public async Task SetMicrophoneEnabledAsync(bool enabled)
    {
        if (Role == Role.Audience)
        {
            throw SfuException.StateError("not_speaker", "audience members talk with SetPushToTalkAsync");
        }

        await _room.SetMicrophoneEnabled(enabled).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts or stops the camera (speakers). The camera m-line is negotiated once, so this needs no renegotiation.
    /// A decoupled room whose camera pool is full refuses it (<see cref="SfuException"/> with code
    /// <c>camera_capacity_full</c>); the camera stays enabled locally and is requested again after a reconnect.
    /// </summary>
    public async Task SetCameraEnabledAsync(bool enabled)
    {
        if (Role == Role.Audience)
        {
            throw SfuException.StateError("camera_not_allowed", "the camera is not allowed for audience members");
        }

        await _room.SetCameraEnabled(enabled).ConfigureAwait(false);
    }

    /// <summary>Holds or releases push-to-talk (audience).</summary>
    public async Task SetPushToTalkAsync(bool active)
    {
        if (Role != Role.Audience)
        {
            throw SfuException.StateError("not_audience", "push-to-talk is for audience members");
        }

        await _room.SetPushToTalk(active).ConfigureAwait(false);
    }

    /// <summary>Replaces this participant's metadata (at most 511 bytes), conventionally <c>"display_name;avatar_url"</c>.</summary>
    public async Task SetMetadataAsync(string metadata) => await _room.SetMetadata(metadata).ConfigureAwait(false);
}
