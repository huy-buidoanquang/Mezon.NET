using System;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>
/// Mute bookkeeping of one signaling connection. Every mute carries a strictly increasing <c>mute_revision</c> (the
/// server rejects lower or equal revisions once one was seen) and is acknowledged by <c>mute_changed</c> echoing
/// it. A moderator mute sends the target no <c>mute_changed</c>; it is only visible as a self <c>peer_updated</c>
/// with <c>is_mute</c>, which becomes a forced-mute signal after a short settle delay unless a local mute is in flight.
/// </summary>
internal sealed class MuteSync
{
    private readonly List<(bool Muted, long Revision)> _pending = new();
    private readonly TimeSpan _settle = TimeSpan.FromMilliseconds(300);
    private long _revision;
    private TimeSpan? _forcedAt;

    public int PendingCount => _pending.Count;

    /// <summary>Registers an outgoing mute and returns the revision to send.</summary>
    public long Next(bool muted)
    {
        _forcedAt = null;
        _revision++;
        _pending.Add((muted, _revision));
        return _revision;
    }

    /// <summary>Handles <c>mute_changed</c>; older pending requests are superseded. Returns whether a pending request matched.</summary>
    public bool Acknowledge(bool muted, long? revision)
    {
        var index = _pending.FindIndex(p => revision is { } r ? p.Revision == r : p.Muted == muted);
        if (index < 0)
        {
            return false;
        }

        _pending.RemoveRange(0, index + 1);
        _forcedAt = null;
        return true;
    }

    /// <summary>Handles a mute error or timeout for the oldest pending request.</summary>
    public void Reject()
    {
        if (_pending.Count > 0)
        {
            _pending.RemoveAt(0);
        }
    }

    /// <summary>Feeds a self <c>peer_updated</c>; <paramref name="localMuted"/> is what the application wants.</summary>
    public void ObserveServerState(bool serverMuted, bool localMuted, TimeSpan now)
    {
        if (_pending.Count > 0 || !serverMuted || localMuted)
        {
            _forcedAt = null;
        }
        else
        {
            _forcedAt ??= Clock.Deadline(now, _settle);
        }
    }

    /// <summary>True (once) when a forced mute has settled.</summary>
    public bool TakeForcedMute(TimeSpan now)
    {
        if (_forcedAt is { } at && _pending.Count == 0 && now >= at)
        {
            _forcedAt = null;
            return true;
        }

        return false;
    }
}

/// <summary>Why the room stops instead of reconnecting.</summary>
internal enum StopReason
{
    Client,
    Kicked,
    DuplicateSession,
    AloneTimeout,
    ServerClosed,
    ServerMisconfigured,
    AuthFailed,
    ProtocolRejected,
    Disabled,
    Exhausted,
}

/// <summary>What to do after a session ended.</summary>
internal readonly record struct ReconnectDecision(bool Reconnect, TimeSpan Delay, bool RefreshToken, StopReason Stop)
{
    public static ReconnectDecision Retry(TimeSpan delay, bool refreshToken) => new(true, delay, refreshToken, default);

    public static ReconnectDecision Halt(StopReason reason) => new(false, TimeSpan.Zero, false, reason);
}

/// <summary>Reconnect limits of the core policy.</summary>
internal sealed record ReconnectLimits(bool Enabled = true, int MaxAttempts = 8, TimeSpan? MaxDuration = null)
{
    public TimeSpan Duration => MaxDuration ?? TimeSpan.FromMinutes(2);
}

/// <summary>Decides whether and when to rejoin after a session ended. One instance tracks one outage; reset it once a session has been stable.</summary>
internal sealed class ReconnectPolicy
{
    private readonly ReconnectLimits _limits;
    private int _attempt;
    private TimeSpan? _outageStart;
    private int _authFailures;
    private int _protocolRejections;

    public ReconnectPolicy(ReconnectLimits limits) => _limits = limits;

    /// <summary>Equal-jitter exponential backoff: 0.5–1 s, 1–2 s, 2–4 s, then 4–8 s. <paramref name="random"/> is in [0, 1].</summary>
    public static TimeSpan DefaultDelay(int attempt, double random)
    {
        var baseSeconds = 1 << Math.Min(Math.Max(attempt, 0), 3);
        return TimeSpan.FromMilliseconds(baseSeconds * 1000 * (0.5 + 0.5 * Math.Min(Math.Max(random, 0), 1)));
    }

    /// <summary>
    /// <paramref name="code"/> is the WebSocket close code, or null when the session failed without a close
    /// (watchdog, ICE, negotiation or token-provider failure): such failures are retryable. <paramref name="delay"/>
    /// computes the wait before attempt n.
    /// </summary>
    public ReconnectDecision Next(int? code, bool clientInitiated, TimeSpan now, Func<int, TimeSpan> delay)
    {
        if (clientInitiated)
        {
            return ReconnectDecision.Halt(StopReason.Client);
        }

        var action = code is { } c ? CloseCodes.ActionFor(c) : CloseAction.Reconnect;
        var refresh = false;
        switch (action)
        {
            case CloseAction.Terminal:
                return ReconnectDecision.Halt(code switch
                {
                    CloseCodes.Kicked => StopReason.Kicked,
                    CloseCodes.AloneTimeout => StopReason.AloneTimeout,
                    CloseCodes.DuplicateSession => StopReason.DuplicateSession,
                    _ => StopReason.ServerClosed,
                });
            case CloseAction.Fatal:
                return ReconnectDecision.Halt(StopReason.ServerMisconfigured);
            case CloseAction.RefreshToken:
                _authFailures++;
                if (_authFailures > 2)
                {
                    return ReconnectDecision.Halt(StopReason.AuthFailed);
                }

                refresh = true;
                break;
            case CloseAction.RetryOnce:
                _protocolRejections++;
                if (_protocolRejections > 1)
                {
                    return ReconnectDecision.Halt(StopReason.ProtocolRejected);
                }

                break;
        }

        if (!_limits.Enabled)
        {
            return ReconnectDecision.Halt(StopReason.Disabled);
        }

        var start = _outageStart ??= now;
        if (_attempt >= _limits.MaxAttempts || now - start >= _limits.Duration)
        {
            return ReconnectDecision.Halt(StopReason.Exhausted);
        }

        var wait = delay(_attempt);
        _attempt++;
        return ReconnectDecision.Retry(wait, refresh);
    }

    /// <summary>Starts a fresh outage budget.</summary>
    public void Reset()
    {
        _attempt = 0;
        _outageStart = null;
        _authFailures = 0;
        _protocolRejections = 0;
    }
}

/// <summary>Speaker detection thresholds (dynamic rooms).</summary>
public sealed record SpeakerOptions
{
    /// <summary>Level (0–1) above which a sample counts as speech. Default 0.1.</summary>
    public double Threshold { get; init; } = 0.1;

    /// <summary>Consecutive loud samples needed to start speaking. Default 2.</summary>
    public int StartSamples { get; init; } = 2;

    /// <summary>Silence needed to stop speaking. Default 1.5 s.</summary>
    public TimeSpan Hold { get; init; } = TimeSpan.FromMilliseconds(1500);
}

/// <summary>Turns periodic audio level samples into a speaking state per participant with hysteresis, and orders current speakers by level.</summary>
internal sealed class SpeakerDetector
{
    private sealed class State
    {
        public int LoudStreak;
        public bool Speaking;
        public TimeSpan? LastLoudAt;
        public double Level;
    }

    private readonly SpeakerOptions _options;
    private readonly Dictionary<uint, State> _states = new();

    public SpeakerDetector(SpeakerOptions options) => _options = options;

    /// <summary>Records one sample per participant (participants missing from <paramref name="levels"/> are forgotten) and returns the speakers, loudest first.</summary>
    public List<uint> Update(IReadOnlyDictionary<uint, double> levels, TimeSpan now)
    {
        foreach (var key in _states.Keys.Where(k => !levels.ContainsKey(k)).ToList())
        {
            _states.Remove(key);
        }

        foreach (var entry in levels)
        {
            if (!_states.TryGetValue(entry.Key, out var state))
            {
                state = new State();
                _states[entry.Key] = state;
            }

            state.Level = entry.Value;
            if (entry.Value >= _options.Threshold)
            {
                state.LoudStreak++;
                state.LastLoudAt = now;
                if (state.LoudStreak >= _options.StartSamples)
                {
                    state.Speaking = true;
                }
            }
            else
            {
                state.LoudStreak = 0;
                if (state.Speaking && (state.LastLoudAt is not { } at || now - at >= _options.Hold))
                {
                    state.Speaking = false;
                }
            }
        }

        return _states.Where(s => s.Value.Speaking)
            .OrderByDescending(s => s.Value.Level)
            .ThenBy(s => s.Key)
            .Select(s => s.Key)
            .ToList();
    }

    public bool IsSpeaking(uint key) => _states.TryGetValue(key, out var s) && s.Speaking;

    public void Clear() => _states.Clear();
}
