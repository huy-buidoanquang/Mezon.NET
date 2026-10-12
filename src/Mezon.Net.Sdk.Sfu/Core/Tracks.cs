using System;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>One remote media stream as the application should see it.</summary>
/// <param name="Epoch">
/// Distinguishes successive occupants of the same mid: the slot generation in fixed mode, or the offer generation
/// that installed the occupant in dynamic mode.
/// </param>
/// <param name="Muted">Audio only: the publisher is muted (fixed mode <c>audio_active: false</c>).</param>
internal sealed record TrackSpec(uint Mid, uint Slot, TrackSource Source, uint PeerId, string UserId, long Epoch, bool Muted);

/// <summary>The publisher holding a fixed slot.</summary>
internal sealed record SlotOccupant(uint PeerId, string UserId, long Generation, bool AudioActive, bool VideoActive, bool ScreenActive);

/// <summary>The effect of a slot assignment.</summary>
internal enum SlotChange
{
    Ignored,
    Assigned,
    Replaced,
    Flags,
}

/// <summary>
/// Fixed-pool downlink: which publisher occupies each slot, guarded by the server's assignment generations so
/// delayed or duplicated slot events cannot resurrect an older occupant.
/// </summary>
internal sealed class SlotMap
{
    private readonly SortedDictionary<uint, SlotOccupant> _slots = new();
    private readonly Dictionary<uint, long> _retired = new();

    /// <summary>Applies <c>slot_assigned</c> (or a snapshot mapping).</summary>
    public SlotChange Assign(uint slot, SlotOccupant occupant)
    {
        if (occupant.Generation <= 0 || occupant.Generation <= _retired.GetValueOrDefault(slot))
        {
            return SlotChange.Ignored;
        }

        var replaced = false;
        if (_slots.TryGetValue(slot, out var current))
        {
            if (occupant.Generation < current.Generation)
            {
                return SlotChange.Ignored;
            }

            if (occupant.Generation == current.Generation && occupant.PeerId == current.PeerId)
            {
                _slots[slot] = occupant;
                return SlotChange.Flags;
            }

            replaced = true;
        }

        _slots[slot] = occupant;
        return replaced ? SlotChange.Replaced : SlotChange.Assigned;
    }

    /// <summary>
    /// Handles <c>slot_released</c>, whose generation is the one being retired. The occupant is removed when its
    /// generation is not newer than the released one.
    /// </summary>
    public SlotOccupant? Release(uint slot, long generation)
    {
        _retired[slot] = Math.Max(_retired.GetValueOrDefault(slot), generation);
        if (_slots.TryGetValue(slot, out var current) && current.Generation <= generation)
        {
            _slots.Remove(slot);
            return current;
        }

        return null;
    }

    /// <summary>Removes every slot held by <paramref name="peerId"/> (the peer left) and returns the freed slots.</summary>
    public List<uint> ReleasePeer(uint peerId)
    {
        var freed = _slots.Where(s => s.Value.PeerId == peerId).Select(s => s.Key).ToList();
        foreach (var slot in freed)
        {
            var occupant = _slots[slot];
            _slots.Remove(slot);
            _retired[slot] = Math.Max(_retired.GetValueOrDefault(slot), occupant.Generation);
        }

        return freed;
    }

    public SlotOccupant? Get(uint slot) => _slots.TryGetValue(slot, out var o) ? o : null;

    /// <summary>Occupied slots in ascending order.</summary>
    public IEnumerable<KeyValuePair<uint, SlotOccupant>> Occupied => _slots;

    public void Clear()
    {
        _slots.Clear();
        _retired.Clear();
    }
}

/// <summary>The publisher of a dynamic mid.</summary>
internal sealed record DynamicOccupant(uint PeerId, string UserId, long Epoch);

/// <summary>
/// Dynamic downlink: who occupies each remote mid, from the <c>a=msid:u&lt;user&gt;-p&lt;peer&gt;</c> of the newest
/// applied offer. Sendonly sections with a msid are occupied; anything else is free.
/// </summary>
internal sealed class DynamicOccupancy
{
    private SortedDictionary<uint, DynamicOccupant> _byMid = new();
    private readonly Dictionary<uint, TimeSpan> _departed = new();

    /// <summary>Departures currently remembered (tests check that they are pruned).</summary>
    public int DepartedCount => _departed.Count;

    public IReadOnlyDictionary<uint, DynamicOccupant> ByMid => _byMid;

    private void Prune(TimeSpan now)
    {
        foreach (var id in _departed.Where(d => d.Value <= now).Select(d => d.Key).ToList())
        {
            _departed.Remove(id);
        }
    }

    /// <summary>Applies an offer's remote sections; <paramref name="epoch"/> is its offer generation.</summary>
    public void ApplyOffer(OfferLayout layout, long epoch, TimeSpan now)
    {
        Prune(now);
        var next = new SortedDictionary<uint, DynamicOccupant>();
        foreach (var section in layout.Remote)
        {
            if (section.Occupant is not Occupant.Peer peer || section.Direction != "sendonly" || _departed.ContainsKey(peer.PeerId))
            {
                continue;
            }

            next[section.Mid] = _byMid.TryGetValue(section.Mid, out var previous) && previous.PeerId == peer.PeerId
                ? previous
                : new DynamicOccupant(peer.PeerId, peer.UserId, epoch);
        }

        _byMid = next;
    }

    /// <summary>Frees the peer's mids and ignores it in offers for <paramref name="ttl"/> (late offers).</summary>
    public void RemovePeer(uint peerId, TimeSpan now, TimeSpan ttl)
    {
        Prune(now);
        _departed[peerId] = Clock.Deadline(now, ttl);
        foreach (var mid in _byMid.Where(o => o.Value.PeerId == peerId).Select(o => o.Key).ToList())
        {
            _byMid.Remove(mid);
        }
    }

    /// <summary>Lets a peer occupy mids again.</summary>
    public void ForgetDeparture(uint peerId) => _departed.Remove(peerId);

    public DynamicOccupant? Get(uint mid) => _byMid.TryGetValue(mid, out var o) ? o : null;

    public void Clear()
    {
        _byMid.Clear();
        _departed.Clear();
    }
}

/// <summary>What changed between the published tracks and the desired ones.</summary>
/// <param name="MuteChanged">Same occupant, only <c>Muted</c> changed.</param>
internal sealed record TrackDiff(List<TrackSpec> Unsubscribed, List<TrackSpec> Subscribed, List<TrackSpec> MuteChanged);

internal static class Tracks
{
    /// <summary>Builds a slot occupant from a membership mapping (snapshot, <c>peer_joined</c>).</summary>
    public static (uint Slot, SlotOccupant Occupant)? OccupantFromMapping(uint peerId, string userId, SlotMapping? mapping, ParticipantState state)
    {
        if (mapping is not { Slot: { } slot } m || m.AssignmentGeneration <= 0)
        {
            return null;
        }

        return (slot, new SlotOccupant(peerId, userId, m.AssignmentGeneration, !state.IsMuted, state.CameraActive, state.ScreenActive));
    }

    /// <summary>
    /// The remote tracks the application should currently have, keyed by mid. Fixed mode: every occupied slot
    /// yields its audio (muted when <c>audio_active</c> is false) plus camera and screen while active. Dynamic
    /// mode: every occupied sendonly mid yields its track. <paramref name="slotCount"/> bounds fixed slots to those
    /// negotiated in the PeerConnection.
    /// </summary>
    public static SortedDictionary<uint, TrackSpec> Desired(DownlinkMode mode, uint slotCount, SlotMap slots, DynamicOccupancy dynamic)
    {
        var result = new SortedDictionary<uint, TrackSpec>();
        if (mode == DownlinkMode.Fixed)
        {
            foreach (var (slot, occupant) in slots.Occupied.Select(kv => (kv.Key, kv.Value)))
            {
                if (slot >= slotCount)
                {
                    continue;
                }

                var first = Sdp.SlotFirstMid(slot);
                TrackSpec Spec(uint mid, TrackSource source, bool muted) => new(mid, slot, source, occupant.PeerId, occupant.UserId, occupant.Generation, muted);
                result[first] = Spec(first, TrackSource.Microphone, !occupant.AudioActive);
                if (occupant.VideoActive)
                {
                    result[first + 1] = Spec(first + 1, TrackSource.Camera, false);
                }

                if (occupant.ScreenActive)
                {
                    result[first + 2] = Spec(first + 2, TrackSource.Screen, false);
                }
            }

            return result;
        }

        foreach (var entry in dynamic.ByMid)
        {
            if (Sdp.RemoteMidInfo(entry.Key) is { } info)
            {
                result[entry.Key] = new TrackSpec(entry.Key, info.Slot, info.Source, entry.Value.PeerId, entry.Value.UserId, entry.Value.Epoch, false);
            }
        }

        return result;
    }

    private static bool SameOccupant(TrackSpec a, TrackSpec b) => a.PeerId == b.PeerId && a.Epoch == b.Epoch;

    /// <summary>Compares published with desired. Unsubscribes come before subscribes; each list is by mid.</summary>
    public static TrackDiff Compare(IReadOnlyDictionary<uint, TrackSpec> published, IReadOnlyDictionary<uint, TrackSpec> desired)
    {
        var diff = new TrackDiff(new List<TrackSpec>(), new List<TrackSpec>(), new List<TrackSpec>());
        foreach (var entry in published.OrderBy(e => e.Key))
        {
            if (!desired.TryGetValue(entry.Key, out var after) || !SameOccupant(entry.Value, after))
            {
                diff.Unsubscribed.Add(entry.Value);
            }
        }

        foreach (var entry in desired.OrderBy(e => e.Key))
        {
            if (published.TryGetValue(entry.Key, out var before) && SameOccupant(before, entry.Value))
            {
                if (before.Muted != entry.Value.Muted)
                {
                    diff.MuteChanged.Add(entry.Value);
                }
            }
            else
            {
                diff.Subscribed.Add(entry.Value);
            }
        }

        return diff;
    }
}
