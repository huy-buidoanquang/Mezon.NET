using System;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>What a client should do after the signaling socket closed.</summary>
public enum CloseAction
{
    /// <summary>Transient; rejoin with backoff.</summary>
    Reconnect,

    /// <summary>The token was rejected; obtain a new one, then rejoin.</summary>
    RefreshToken,

    /// <summary>Protocol-level rejection; one rejoin, then give up.</summary>
    RetryOnce,

    /// <summary>Rejoining cannot help (server misconfiguration).</summary>
    Fatal,

    /// <summary>The session ended on purpose (kicked, replaced, alone, normal close).</summary>
    Terminal,
}

/// <summary>
/// WebSocket close codes of mezon-sfu (<c>mezon-sfu/src/protocol/signaling/signaling.{h,c}</c>), with what a
/// client should do about each.
/// </summary>
public static class CloseCodes
{
    public const int Normal = 1000;
    public const int GoingAway = 1001;
    public const int ProtocolError = 1002;

    /// <summary>Set by the client's WebSocket when the connection drops without a close frame; the server never sends it.</summary>
    public const int Abnormal = 1006;

    public const int PolicyViolation = 1008;
    public const int InternalError = 1011;
    public const int IdleTimeout = 4001;
    public const int PingFailed = 4002;
    public const int AuthNotConfigured = 4003;
    public const int MissingToken = 4004;
    public const int InvalidToken = 4005;
    public const int Kicked = 4006;
    public const int WsHandshakeFailed = 4007;
    public const int RecvError = 4008;
    public const int PollStartFailed = 4009;
    public const int TransportError = 4010;
    public const int AloneTimeout = 4011;

    /// <summary>The same user joined from another connection, in any room.</summary>
    public const int DuplicateSession = 4012;

    public const int DtlsFailed = 4013;
    public const int DtlsHandshakeTimeout = 4014;

    /// <summary>Every close code the server sends (plus 1006), with its name and action.</summary>
    public static IReadOnlyList<(int Code, string Name, CloseAction Action)> All { get; } = new[]
    {
        (Normal, "NORMAL", CloseAction.Terminal),
        (GoingAway, "GOING_AWAY", CloseAction.Reconnect),
        (ProtocolError, "PROTOCOL_ERROR", CloseAction.RetryOnce),
        (Abnormal, "ABNORMAL", CloseAction.Reconnect),
        (PolicyViolation, "POLICY_VIOLATION", CloseAction.RetryOnce),
        (InternalError, "INTERNAL_ERROR", CloseAction.Reconnect),
        (IdleTimeout, "IDLE_TIMEOUT", CloseAction.Reconnect),
        (PingFailed, "PING_FAILED", CloseAction.Reconnect),
        (AuthNotConfigured, "AUTH_NOT_CONFIGURED", CloseAction.Fatal),
        (MissingToken, "MISSING_TOKEN", CloseAction.RefreshToken),
        (InvalidToken, "INVALID_TOKEN", CloseAction.RefreshToken),
        (Kicked, "KICKED", CloseAction.Terminal),
        (WsHandshakeFailed, "WS_HANDSHAKE_FAILED", CloseAction.Reconnect),
        (RecvError, "RECV_ERROR", CloseAction.Reconnect),
        (PollStartFailed, "POLL_START_FAILED", CloseAction.Reconnect),
        (TransportError, "TRANSPORT_ERROR", CloseAction.Reconnect),
        (AloneTimeout, "ALONE_TIMEOUT", CloseAction.Terminal),
        (DuplicateSession, "DUPLICATE_SESSION", CloseAction.Terminal),
        (DtlsFailed, "DTLS_FAILED", CloseAction.Reconnect),
        (DtlsHandshakeTimeout, "DTLS_HANDSHAKE_TIMEOUT", CloseAction.Reconnect),
    };

    /// <summary>What to do after the socket closed with <paramref name="code"/>. Unknown codes reconnect.</summary>
    public static CloseAction ActionFor(int code)
    {
        foreach (var entry in All)
        {
            if (entry.Code == code)
            {
                return entry.Action;
            }
        }

        return CloseAction.Reconnect;
    }

    /// <summary>The name of a close code (<c>"KICKED"</c>), or null for codes the server never sends.</summary>
    public static string? NameOf(int code) => All.FirstOrDefault(c => c.Code == code).Name;

    /// <summary>The spelling used by <c>mezon-sfu/docs/protocol/close-codes.json</c>.</summary>
    public static string ToSpecString(this CloseAction action) => action switch
    {
        CloseAction.Reconnect => "reconnect",
        CloseAction.RefreshToken => "refresh-token",
        CloseAction.RetryOnce => "retry-once",
        CloseAction.Fatal => "fatal",
        _ => "terminal",
    };
}

/// <summary>A server error code (<c>{"type":"error","message":&lt;code&gt;}</c>).</summary>
/// <param name="Sources">Client message types whose handler can reply with this code.</param>
/// <param name="Retryable">Repeating the operation later may succeed.</param>
/// <param name="Fatal">The connection cannot continue usefully.</param>
public sealed record ServerErrorInfo(string Code, IReadOnlyList<string> Sources, bool Retryable, bool Fatal);

/// <summary>Every error code mezon-sfu can send, with the requests that produce it.</summary>
public static class ServerErrors
{
    private static readonly string[] Session = { "mute", "camera", "share_screen", "visibility", "request_keyframe", "peer_update" };

    private static ServerErrorInfo E(string code, string[] sources, bool retryable, bool fatal) => new(code, sources, retryable, fatal);

    public static IReadOnlyList<ServerErrorInfo> All { get; } = new[]
    {
        E("already_joined", new[] { "join" }, false, false),
        E("auth_not_configured", new[] { "join", "participant_action" }, false, true),
        E("missing_token", new[] { "join" }, false, true),
        E("invalid_token", new[] { "join", "participant_action" }, false, true),
        E("invalid_room", new[] { "join" }, false, true),
        E("room_creation_failed", new[] { "join" }, true, true),
        E("unsupported_media_pool_version", new[] { "join" }, false, true),
        E("must_join_room_first", new[] { "answer", "mute", "visibility", "request_keyframe", "send_message", "peer_update", "participant_action" }, true, false),
        E("missing_offer_generation", new[] { "answer" }, false, false),
        E("invalid_answer_ufrag", new[] { "answer" }, false, true),
        E("invalid_answer_sdp", new[] { "answer" }, false, false),
        E("stale_offer_generation", new[] { "answer" }, true, false),
        E("future_offer_generation", new[] { "answer" }, true, false),
        E("duplicate_ice_ufrag", new[] { "answer" }, false, true),
        E("routing_table_full", new[] { "answer" }, true, true),
        E("routing_registration_failed", new[] { "answer" }, true, true),
        E("room_capacity", new[] { "answer" }, false, true),
        E("session_not_found", Session, true, false),
        E("invalid_mute", new[] { "mute" }, false, false),
        E("stale_mute_revision", new[] { "mute" }, false, false),
        E("invalid_camera_active", new[] { "camera" }, true, false),
        E("camera_not_allowed", new[] { "camera" }, false, false),
        E("camera_capacity_full", new[] { "camera" }, true, false),
        E("camera_update_failed", new[] { "camera" }, true, false),
        E("invalid_screen_share_active", new[] { "share_screen" }, true, false),
        E("screen_share_not_allowed", new[] { "share_screen" }, false, false),
        E("screen_share_capacity_full", new[] { "share_screen" }, true, false),
        E("screen_share_update_failed", new[] { "share_screen" }, true, false),
        E("invalid_push_to_talk", new[] { "push_to_talk" }, true, false),
        E("push_to_talk_rejected", new[] { "push_to_talk" }, true, false),
        E("role_change_disabled", Array.Empty<string>(), false, false),
        E("invalid_visibility", new[] { "visibility" }, false, false),
        E("invalid_kind", new[] { "request_keyframe" }, false, false),
        E("invalid_publisher_id", new[] { "request_keyframe" }, false, false),
        E("publisher_not_found", new[] { "request_keyframe" }, true, false),
        E("recovery_queue_full", new[] { "request_keyframe" }, true, false),
        E("invalid_message", new[] { "send_message" }, false, false),
        E("invalid_participant_action", new[] { "participant_action" }, false, false),
        E("token_room_mismatch", new[] { "participant_action" }, false, false),
        E("target_not_found", new[] { "participant_action" }, false, false),
        E("unsupported_participant_action", new[] { "participant_action" }, false, false),
    };

    /// <summary>Metadata for a server error code, or null for codes unknown to this version.</summary>
    public static ServerErrorInfo? Find(string code) => All.FirstOrDefault(e => e.Code == code);
}
