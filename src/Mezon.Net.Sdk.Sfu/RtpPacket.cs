using System;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>A received RTP packet as forwarded by the SFU (Opus or VP8/VP9 payload, not decoded).</summary>
public sealed class RtpPacket
{
    public RtpPacket(byte payloadType, ushort sequenceNumber, uint timestamp, uint ssrc, bool marker, ReadOnlyMemory<byte> payload)
    {
        PayloadType = payloadType;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
        Ssrc = ssrc;
        Marker = marker;
        Payload = payload;
    }

    public byte PayloadType { get; }

    public ushort SequenceNumber { get; }

    public uint Timestamp { get; }

    public uint Ssrc { get; }

    public bool Marker { get; }

    public ReadOnlyMemory<byte> Payload { get; }
}
