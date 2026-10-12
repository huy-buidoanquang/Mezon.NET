using System;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>What kind of failure an <see cref="SfuException"/> reports.</summary>
public enum SfuErrorKind
{
    /// <summary>The operation needs a connected room.</summary>
    NotConnected,

    /// <summary>A call that is not valid now (wrong role, wrong state, invalid argument).</summary>
    State,

    /// <summary>A server error frame, attributed to the request it answered when possible.</summary>
    Signaling,

    /// <summary>The server did not acknowledge in time, or a session watchdog fired.</summary>
    Timeout,

    /// <summary>The session ended while the operation was pending.</summary>
    Disconnected,

    /// <summary>No usable meet token could be obtained.</summary>
    Authentication,

    /// <summary>An offer could not be answered, or ICE/DTLS failed.</summary>
    Negotiation,

    /// <summary>A client message could not be encoded.</summary>
    Protocol,

    /// <summary>The signaling socket failed.</summary>
    Socket,

    /// <summary>The WebRTC stack failed.</summary>
    WebRtc,
}

/// <summary>An error of the SFU SDK.</summary>
public sealed class SfuException : Exception
{
    public SfuException(SfuErrorKind kind, string code, string message, string? operation = null, bool retryable = false, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        Code = code;
        Operation = operation;
        Retryable = retryable;
    }

    public SfuErrorKind Kind { get; }

    /// <summary>
    /// A machine-readable code: the server error code (<see cref="SfuErrorKind.Signaling"/>), the timed-out
    /// operation (<see cref="SfuErrorKind.Timeout"/>), the end reason (<see cref="SfuErrorKind.Disconnected"/>), or an
    /// SDK code such as <c>not_speaker</c> or <c>token_provider_failed</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>The client message type a <see cref="SfuErrorKind.Signaling"/> error answered, when known.</summary>
    public string? Operation { get; }

    /// <summary>Repeating the operation later may succeed.</summary>
    public bool Retryable { get; }

    internal static SfuException NotConnected() => new(SfuErrorKind.NotConnected, "not_connected", "the room is not connected");

    internal static SfuException StateError(string code, string message) => new(SfuErrorKind.State, code, message);

    internal static SfuException Signaling(string code, string? operation, bool retryable) =>
        new(SfuErrorKind.Signaling, code, operation is null ? $"server error: {code}" : $"server rejected {operation}: {code}", operation, retryable);

    internal static SfuException Timeout(string operation) => new(SfuErrorKind.Timeout, operation, $"timed out: {operation}");

    internal static SfuException Disconnected(string reason) => new(SfuErrorKind.Disconnected, reason, $"disconnected: {reason}");

    internal static SfuException Authentication(string code, string message, Exception? inner = null) =>
        new(SfuErrorKind.Authentication, code, $"authentication failed ({code}): {message}", inner: inner);

    internal static SfuException Negotiation(string code, string message, Exception? inner = null) =>
        new(SfuErrorKind.Negotiation, code, $"negotiation failed ({code}): {message}", inner: inner);

    internal static SfuException Protocol(string message, Exception? inner = null) => new(SfuErrorKind.Protocol, "protocol", message, inner: inner);

    internal static SfuException Socket(string message, Exception? inner = null) => new(SfuErrorKind.Socket, "socket", $"signaling socket: {message}", inner: inner);

    internal static SfuException WebRtc(string message, Exception? inner = null) => new(SfuErrorKind.WebRtc, "webrtc", $"webrtc: {message}", inner: inner);
}
