using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>Receive counters of a <see cref="RemoteTrack"/>.</summary>
/// <param name="PacketsDropped">Packets discarded because the reader fell behind.</param>
public readonly record struct TrackStats(long PacketsReceived, long BytesReceived, long PacketsDropped);

/// <summary>
/// One remote media stream: a participant's microphone, camera or screen. Read its RTP with
/// <see cref="ReadRtpAsync"/> (or <see cref="ReadAllAsync"/>) on your own task; packets that are not read are dropped
/// once <see cref="BufferSize"/> are buffered. After the track is unsubscribed, reading returns null (ends).
/// Thread-safe; instances are compared by reference.
/// </summary>
public sealed class RemoteTrack
{
    /// <summary>Packets buffered per track before the oldest are dropped.</summary>
    public const int BufferSize = 512;

    private readonly Channel<RtpPacket> _queue;
    private readonly object _levelLock = new();
    private long _received;
    private long _bytes;
    private long _dropped;
    private int _muted;
    private int _ended;
    private string? _mimeType;
    private long _levelBytes;
    private TimeSpan? _levelAt;

    internal RemoteTrack(TrackSpec spec)
    {
        Spec = spec;
        Sid = $"m{spec.Mid}-p{spec.PeerId}-e{spec.Epoch}";
        _muted = spec.Muted ? 1 : 0;
        _queue = Channel.CreateBounded<RtpPacket>(
            new BoundedChannelOptions(BufferSize) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = false, SingleWriter = true },
            _ => Interlocked.Increment(ref _dropped));
    }

    internal TrackSpec Spec { get; }

    /// <summary>Identifies this subscription: mid, publisher and occupancy epoch.</summary>
    public string Sid { get; }

    /// <summary>The m-line the track arrives on.</summary>
    public uint Mid => Spec.Mid;

    public TrackSource Source => Spec.Source;

    public uint PeerId => Spec.PeerId;

    public string UserId => Spec.UserId;

    /// <summary>Whether the publisher muted this (audio) track (fixed-slot rooms).</summary>
    public bool IsMuted => Volatile.Read(ref _muted) != 0;

    /// <summary>Whether the track was unsubscribed.</summary>
    public bool IsEnded => Volatile.Read(ref _ended) != 0;

    /// <summary>The codec MIME type (<c>audio/opus</c>, <c>video/VP8</c>, ...), once the first packet arrived.</summary>
    public string? MimeType => Volatile.Read(ref _mimeType);

    public TrackStats Stats => new(Interlocked.Read(ref _received), Interlocked.Read(ref _bytes), Interlocked.Read(ref _dropped));

    /// <summary>The next packet; null once the track is unsubscribed and its buffer drained.</summary>
    public async ValueTask<RtpPacket?> ReadRtpAsync(CancellationToken cancellationToken = default)
    {
        while (await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_queue.Reader.TryRead(out var packet))
            {
                return packet;
            }
        }

        return null;
    }

    /// <summary>Every packet until the track is unsubscribed.</summary>
    public async IAsyncEnumerable<RtpPacket> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await ReadRtpAsync(cancellationToken).ConfigureAwait(false) is { } packet)
        {
            yield return packet;
        }
    }

    public override string ToString() => $"RemoteTrack({Sid}, {Source})";

    internal void SetMuted(bool muted) => Volatile.Write(ref _muted, muted ? 1 : 0);

    internal void Deliver(RtpPacket packet, string? mimeType)
    {
        if (IsEnded)
        {
            return;
        }

        if (mimeType is not null && _mimeType is null)
        {
            Interlocked.CompareExchange(ref _mimeType, mimeType, null);
        }

        Interlocked.Increment(ref _received);
        Interlocked.Add(ref _bytes, packet.Payload.Length);
        _queue.Writer.TryWrite(packet);
    }

    internal void End()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0)
        {
            _queue.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Estimated audio level (0–1) from the Opus payload bitrate since the previous sample: the SFU forwards no audio
    /// level, and Opus spends far more bits on speech than on silence.
    /// </summary>
    internal double SampleLevel(TimeSpan now)
    {
        var total = Interlocked.Read(ref _bytes);
        lock (_levelLock)
        {
            var previous = _levelBytes;
            var at = _levelAt;
            _levelBytes = total;
            _levelAt = now;
            if (at is null)
            {
                return 0;
            }

            var elapsed = (now - at.Value).TotalSeconds;
            if (elapsed <= 0)
            {
                return 0;
            }

            return Math.Min(Math.Max(((total - previous) / elapsed - 400.0) / 3600.0, 0), 1);
        }
    }
}
