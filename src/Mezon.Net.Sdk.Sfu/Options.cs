using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>Why a meet token is requested.</summary>
public enum TokenReason
{
    Initial,
    Reconnect,

    /// <summary>The current token expires within a minute.</summary>
    Expiring,

    /// <summary>The server rejected the previous token (close code 4004/4005).</summary>
    Rejected,
}

/// <summary>Passed to a <see cref="TokenProvider"/>.</summary>
/// <param name="Previous">The token used last (the rejected one for <see cref="TokenReason.Rejected"/>), if any.</param>
public sealed record TokenRequest(TokenReason Reason, string? Previous);

/// <summary>Supplies meet tokens: before the first join and whenever a fresh one is needed.</summary>
public sealed class TokenProvider
{
    private readonly Func<TokenRequest, CancellationToken, Task<string>> _fetch;

    private TokenProvider(Func<TokenRequest, CancellationToken, Task<string>> fetch, bool isFixed = false)
    {
        _fetch = fetch;
        IsFixed = isFixed;
    }

    internal bool IsFixed { get; }

    /// <summary>A provider backed by an async function (for example one calling the Mezon API's GenerateMeetToken).</summary>
    public static TokenProvider Create(Func<TokenRequest, CancellationToken, Task<string>> fetch) =>
        new(fetch ?? throw new ArgumentNullException(nameof(fetch)));

    /// <summary>
    /// One fixed token. A rejected token cannot be replaced, so a 4005 ends the room with
    /// <see cref="DisconnectReason.AuthFailed"/>.
    /// </summary>
    public static TokenProvider Fixed(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentException("the token is empty", nameof(token));
        }

        return new TokenProvider(
            (request, _) => request.Reason == TokenReason.Rejected
                ? Task.FromException<string>(new InvalidOperationException("token rejected and no provider"))
                : Task.FromResult(token),
            isFixed: true);
    }

    internal Task<string> GetAsync(TokenRequest request, CancellationToken cancellationToken) => _fetch(request, cancellationToken);
}

/// <summary>
/// Returns a moderation token for a user: a meet token for that user whose metadata is the action ("mute" or
/// "kick"), issued by the Mezon API.
/// </summary>
public sealed class ModerationTokenProvider
{
    private readonly Func<string, string, CancellationToken, Task<string>> _fetch;

    private ModerationTokenProvider(Func<string, string, CancellationToken, Task<string>> fetch) => _fetch = fetch;

    /// <param name="fetch">Called with the action ("mute" or "kick") and the target user id.</param>
    public static ModerationTokenProvider Create(Func<string, string, CancellationToken, Task<string>> fetch) =>
        new(fetch ?? throw new ArgumentNullException(nameof(fetch)));

    internal Task<string> GetAsync(string action, string userId, CancellationToken cancellationToken) => _fetch(action, userId, cancellationToken);
}

/// <summary>Automatic rejoin after a dropped session.</summary>
public sealed record ReconnectOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Attempts per outage. Default 8.</summary>
    public int MaxAttempts { get; init; } = 8;

    /// <summary>Duration of one outage. Default 2 minutes.</summary>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Delay before attempt n (0-based). Default: equal-jitter backoff, 0.5–1 s doubling to 4–8 s.</summary>
    public Func<int, TimeSpan>? Delay { get; init; }
}

/// <summary>Timeouts of the connection steps. Negative or huge values mean no timeout.</summary>
public sealed record Timeouts
{
    /// <summary>Wait for the token provider. Default 10 s.</summary>
    public TimeSpan Token { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>WebSocket handshake. Default 10 s.</summary>
    public TimeSpan Dial { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Wait for <c>joined</c> after sending join. Default 10 s.</summary>
    public TimeSpan Join { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>From <c>joined</c> until the snapshot arrived and media connected. Default 20 s.</summary>
    public TimeSpan Admission { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>End a session whose socket received nothing for this long (the server pings every 10 s). Default 25 s.</summary>
    public TimeSpan InboundIdle { get; init; } = TimeSpan.FromSeconds(25);

    /// <summary>Wait for the server to acknowledge a request. Default 8 s.</summary>
    public TimeSpan Operation { get; init; } = TimeSpan.FromSeconds(8);
}

/// <summary>How the ICE servers announced by the SFU combine with <see cref="RoomOptions.IceServers"/>.</summary>
public enum IceServersPolicy
{
    /// <summary>The servers announced in <c>joined</c> (default without <see cref="RoomOptions.IceServers"/>).</summary>
    FromServer,

    /// <summary>Both (default with <see cref="RoomOptions.IceServers"/>).</summary>
    Merge,

    /// <summary>Only <see cref="RoomOptions.IceServers"/>.</summary>
    Override,
}

/// <summary>A STUN/TURN server.</summary>
public sealed record IceServer(IReadOnlyList<string> Urls, string? Username = null, string? Credential = null);

/// <summary>Room configuration.</summary>
public sealed record RoomOptions
{
    public Role Role { get; init; } = Role.Speaker;

    /// <summary>Codec offered for this client's screen uplink. Default VP8.</summary>
    public ScreenCodec ScreenCodec { get; init; } = ScreenCodec.Vp8;

    public IReadOnlyList<IceServer> IceServers { get; init; } = Array.Empty<IceServer>();

    /// <summary>Defaults to <see cref="IceServersPolicy.Merge"/> when <see cref="IceServers"/> is set, else <see cref="IceServersPolicy.FromServer"/>.</summary>
    public IceServersPolicy? IceServersPolicy { get; init; }

    public ReconnectOptions Reconnect { get; init; } = new();

    public Timeouts Timeouts { get; init; } = new();

    /// <summary>
    /// Active speaker detection in dynamic rooms (the level is estimated from the Opus bitrate, because the SFU
    /// forwards no audio level).
    /// </summary>
    public SpeakerOptions ActiveSpeakers { get; init; } = new();

    /// <summary>Needed by <see cref="Room.MuteParticipantAsync"/> and <see cref="Room.KickParticipantAsync"/>.</summary>
    public ModerationTokenProvider? ModerationTokens { get; init; }

    /// <summary>
    /// Local address the WebRTC socket binds to. Default: every interface. Set <see cref="IPAddress.Loopback"/>
    /// when the SFU runs on this machine's 127.0.0.1 (for example a local development stack).
    /// </summary>
    public IPAddress? BindAddress { get; init; }

    /// <summary>Logs of the SDK (category <c>Mezon.Net.Sdk.Sfu</c>). Tokens are never logged.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Creates the PeerConnection of a session; tests replace it with a fake.</summary>
    internal Func<PeerConfig, CancellationToken, Task<IPeerConnection>>? PeerFactory { get; init; }

    /// <summary>Tests only: awaited by <see cref="Room.ConnectAsync"/> after an admitted session, before the result is read.</summary>
    internal Func<Task>? AdmissionProbe { get; init; }

    /// <summary>How long a session must stay up before the reconnect budget is reset; tests shorten it.</summary>
    internal TimeSpan StableAfter { get; init; } = TimeSpan.FromSeconds(30);

    internal IceServersPolicy EffectiveIcePolicy =>
        IceServersPolicy ?? (IceServers.Count == 0 ? Sfu.IceServersPolicy.FromServer : Sfu.IceServersPolicy.Merge);
}
