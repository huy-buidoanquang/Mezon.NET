using System;
using System.Threading;
using Mezon.Net.Sdk.Sfu.Core;
using Mezon.Net.Sdk.Sfu.Peer;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>
/// The microphone uplink: write Opus frames (one frame per packet, 48 kHz RTP clock). Frames written while the
/// microphone is disabled (or push-to-talk is released), or while no session is negotiated, are dropped. The same
/// instance survives reconnects. Thread-safe.
/// </summary>
public sealed class LocalAudioTrack
{
    private const double OpusClockRate = 48_000;

    private readonly object _lock = new();
    private IAudioUplink? _uplink;
    private uint _timestamp;
    private bool _resumed;
    private int _enabled;
    private long _sent;

    internal LocalAudioTrack()
    {
    }

    /// <summary>Whether written frames are sent.</summary>
    public bool IsEnabled => Volatile.Read(ref _enabled) != 0;

    /// <summary>Frames handed to the WebRTC stack so far.</summary>
    public long FramesSent => Interlocked.Read(ref _sent);

    /// <summary>
    /// Sends one Opus frame. <paramref name="level"/> is the RFC 6464 audio level, 0 (loudest) to 127 (silence) in
    /// -dBov; levels below 127 set the voice activity flag. mezon-sfu's voice activity detection uses it to pick
    /// active speakers in fixed-slot rooms; without it this publisher counts as speaking whenever audio flows.
    /// </summary>
    /// <returns>Whether the frame was sent (false while disabled or not negotiated).</returns>
    public bool WriteOpus(ReadOnlySpan<byte> frame, TimeSpan duration, byte? level = null)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var samples = (uint)Math.Max(0, Math.Round(duration.TotalSeconds * OpusClockRate));
        var payload = frame.ToArray();
        lock (_lock)
        {
            if (_uplink is not { } uplink)
            {
                return false;
            }

            byte wireLevel = 0;
            var voice = true;
            if (level is { } l)
            {
                wireLevel = Math.Min(l, (byte)127);
                voice = wireLevel < 127;
            }

            uplink.SendOpus(payload, _timestamp, _resumed, wireLevel, voice);
            _resumed = false;
            _timestamp = unchecked(_timestamp + samples);
            Interlocked.Increment(ref _sent);
            return true;
        }
    }

    internal void SetEnabled(bool enabled)
    {
        var was = Interlocked.Exchange(ref _enabled, enabled ? 1 : 0) != 0;
        if (!was && enabled)
        {
            lock (_lock)
            {
                _resumed = true;
            }
        }
    }

    internal void Bind(IAudioUplink? uplink)
    {
        lock (_lock)
        {
            if (uplink is null)
            {
                _uplink = null;
                return;
            }

            if (!ReferenceEquals(uplink, _uplink))
            {
                _uplink = uplink;
                _timestamp = Rand.NextUInt32();
                _resumed = true;
            }
        }
    }
}

/// <summary>
/// The camera uplink (speakers): write VP8 frames (one frame per call, 90 kHz RTP clock). Frames written while the
/// camera is disabled are dropped. Keyframe requests from receivers are not surfaced, so send keyframes
/// periodically. The same instance survives reconnects. Thread-safe.
/// </summary>
public sealed class LocalVideoTrack
{
    private const double VideoClockRate = 90_000;

    private readonly object _lock = new();
    private IVideoUplink? _uplink;
    private int _enabled;
    private long _sent;

    internal LocalVideoTrack()
    {
    }

    /// <summary>Whether written frames are sent.</summary>
    public bool IsEnabled => Volatile.Read(ref _enabled) != 0;

    /// <summary>Frames handed to the WebRTC stack so far.</summary>
    public long FramesSent => Interlocked.Read(ref _sent);

    /// <summary>Sends one VP8 frame.</summary>
    /// <returns>Whether the frame was sent (false while disabled or not negotiated).</returns>
    public bool WriteVp8(ReadOnlySpan<byte> frame, TimeSpan duration)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var units = (uint)Math.Max(0, Math.Round(duration.TotalSeconds * VideoClockRate));
        var payload = frame.ToArray();
        lock (_lock)
        {
            if (_uplink is not { } uplink)
            {
                return false;
            }

            uplink.SendVp8(payload, units);
            Interlocked.Increment(ref _sent);
            return true;
        }
    }

    internal void SetEnabled(bool enabled) => Volatile.Write(ref _enabled, enabled ? 1 : 0);

    internal void Bind(IVideoUplink? uplink)
    {
        lock (_lock)
        {
            _uplink = uplink;
        }
    }
}
