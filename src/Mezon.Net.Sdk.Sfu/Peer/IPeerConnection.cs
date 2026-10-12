using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Sdk.Sfu.Peer;

/// <summary>A connection state the session cares about.</summary>
internal enum PeerState
{
    New,
    Connecting,
    Connected,
    Disconnected,
    Failed,
    Closed,
}

/// <summary>
/// The negotiated microphone uplink. Opus is one frame per packet; the caller supplies the RTP timestamp and
/// marker, the uplink the sequence number, SSRC, payload type and the RFC 6464 audio-level extension.
/// </summary>
internal interface IAudioUplink
{
    void SendOpus(byte[] frame, uint timestamp, bool marker, byte level, bool voice);
}

/// <summary>The negotiated camera uplink (VP8, packetized by the uplink).</summary>
internal interface IVideoUplink
{
    void SendVp8(byte[] frame, uint durationRtpUnits);
}

/// <summary>What a session needs to create a PeerConnection.</summary>
internal sealed class PeerConfig
{
    public IReadOnlyList<IceServer> IceServers { get; init; } = Array.Empty<IceServer>();

    public IPAddress? BindAddress { get; init; }

    /// <summary>Add a camera uplink (speakers).</summary>
    public bool Camera { get; init; }

    public ILogger Logger { get; init; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public Action<PeerState> OnState { get; init; } = _ => { };

    /// <summary>One received RTP packet on a remote mid, with the codec MIME type of its payload type when known.</summary>
    public Action<uint, RtpPacket, string?> OnRtp { get; init; } = (_, _, _) => { };
}

/// <summary>The PeerConnection operations a session uses. <see cref="SipsorceryPeer"/> is the real one; tests use a fake.</summary>
internal interface IPeerConnection
{
    Task SetRemoteOfferAsync(string sdp);

    Task<string> CreateAnswerAsync();

    Task SetLocalAnswerAsync(string sdp);

    Task RollbackAsync();

    string LocalSdp { get; }

    /// <summary>Remote mids (&gt;= 3) with a transceiver that can receive.</summary>
    IReadOnlyCollection<uint> ReceivingMids();

    /// <summary>The negotiated microphone and camera uplinks.</summary>
    (IAudioUplink? Microphone, IVideoUplink? Camera) Uplinks();

    Task CloseAsync();
}
