using System;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>What the room knows about one remote participant session.</summary>
/// <param name="Mapping">The latest slot/mid mapping announced for this peer, relative to this client.</param>
internal sealed record MemberRecord(ParticipantState State, string Ufrag, SlotMapping? Mapping);

/// <summary>One membership change.</summary>
internal abstract record MemberEvent
{
    public sealed record Joined(MemberRecord Member) : MemberEvent;

    /// <summary>The peer left; <see cref="Mapping"/> is the mapping it held.</summary>
    public sealed record Left(MemberRecord Member, SlotMapping? Mapping) : MemberEvent;

    public sealed record Updated(MemberRecord Member, MemberRecord Previous) : MemberEvent;

    /// <summary>This client's own server state.</summary>
    public sealed record Own(ParticipantState State) : MemberEvent;

    public sealed record Count(int Value) : MemberEvent;
}

/// <summary>
/// Room membership reducer: applies <c>room_snapshot</c>, <c>peer_joined</c>, <c>peer_left</c> and
/// <c>peer_updated</c> and returns the resulting events. Pure; the caller supplies (monotonic) time.
/// </summary>
/// <remarks>
/// The server sends membership deltas, slot events and <c>peer_updated</c> from different queues, so they can
/// arrive out of causal order:
/// <list type="bullet">
/// <item>deltas before the first <c>room_snapshot</c> are buffered and replayed after it when newer;</item>
/// <item><c>peer_joined</c>/<c>peer_left</c> with <c>room_revision</c> &lt;= the current revision are dropped;</item>
/// <item><c>peer_updated</c> (which has no revision) for an unknown peer is kept as an orphan and applied when that
/// peer joins; for a peer that left recently (tombstoned) it is dropped.</item>
/// </list>
/// </remarks>
internal sealed class Membership
{
    private static readonly TimeSpan OrphanTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TombstoneTtl = TimeSpan.FromSeconds(30);

    private readonly SortedDictionary<uint, MemberRecord> _members = new();
    private readonly List<ServerMessage> _buffered = new();
    private readonly Dictionary<uint, (ParticipantState State, TimeSpan At)> _orphans = new();
    private readonly Dictionary<uint, TimeSpan> _tombstones = new();
    private uint? _selfId;
    private long? _revision;

    public ParticipantState? OwnState { get; private set; }

    public bool HasSnapshot => _revision.HasValue;

    /// <summary>The server's participant count, this client included.</summary>
    public int ParticipantCount { get; private set; }

    public IReadOnlyDictionary<uint, MemberRecord> Members => _members;

    /// <summary>Tombstones currently kept (tests check that they are pruned).</summary>
    public int TombstoneCount => _tombstones.Count;

    /// <summary>Records the session id assigned by <c>joined</c>, to recognize self before the snapshot.</summary>
    public void SetSelfPeerId(uint peerId) => _selfId = peerId;

    /// <summary>
    /// Forgets the session but keeps the remote members, so the next session's snapshot is diffed against them:
    /// other peers keep their ids when this client reconnects.
    /// </summary>
    public void ResetSession()
    {
        _selfId = null;
        _revision = null;
        OwnState = null;
        _buffered.Clear();
        _orphans.Clear();
    }

    /// <summary>Drops every member, returning a <see cref="MemberEvent.Left"/> for each.</summary>
    public List<MemberEvent> Clear()
    {
        var events = _members.Values.Select(m => (MemberEvent)new MemberEvent.Left(m, m.Mapping)).ToList();
        _members.Clear();
        ResetSession();
        _tombstones.Clear();
        return events;
    }

    private static MemberRecord ToRecord(Member member) => new(member.State, member.Ufrag, member.Mapping.IsMapped ? member.Mapping : null);

    public List<MemberEvent> ApplySnapshot(ServerMessage.RoomSnapshot msg, TimeSpan now)
    {
        Prune(now);
        var events = new List<MemberEvent>();
        _selfId = msg.SelfPeerId;
        _revision = msg.RoomRevision;
        var seen = new HashSet<uint>();
        foreach (var member in msg.Members)
        {
            if (member.State.PeerId == msg.SelfPeerId)
            {
                OwnState = member.State;
                events.Add(new MemberEvent.Own(member.State));
                continue;
            }

            seen.Add(member.State.PeerId);
            events.AddRange(Upsert(ToRecord(member)));
        }

        foreach (var id in _members.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            var member = _members[id];
            _members.Remove(id);
            _tombstones[id] = Clock.Deadline(now, TombstoneTtl);
            events.Add(new MemberEvent.Left(member, member.Mapping));
        }

        events.AddRange(SetCount(msg.ParticipantCount));
        var buffered = _buffered.ToList();
        _buffered.Clear();
        foreach (var delta in buffered)
        {
            switch (delta)
            {
                case ServerMessage.PeerJoined joined when joined.RoomRevision > msg.RoomRevision:
                    events.AddRange(ApplyJoined(joined, now));
                    break;
                case ServerMessage.PeerLeft left when left.RoomRevision > msg.RoomRevision:
                    events.AddRange(ApplyLeft(left, now));
                    break;
            }
        }

        return events;
    }

    public List<MemberEvent> ApplyJoined(ServerMessage.PeerJoined msg, TimeSpan now)
    {
        if (_revision is not { } revision)
        {
            _buffered.Add(msg);
            return new List<MemberEvent>();
        }

        if (msg.RoomRevision <= revision)
        {
            return new List<MemberEvent>();
        }

        _revision = msg.RoomRevision;
        Prune(now);
        var events = new List<MemberEvent>();
        var id = msg.Peer.State.PeerId;
        if (_selfId != id)
        {
            _tombstones.Remove(id);
            var record = ToRecord(msg.Peer);
            if (_orphans.TryGetValue(id, out var orphan))
            {
                _orphans.Remove(id);
                record = record with { State = orphan.State };
            }

            events.AddRange(Upsert(record));
        }

        events.AddRange(SetCount(msg.ParticipantCount));
        return events;
    }

    public List<MemberEvent> ApplyLeft(ServerMessage.PeerLeft msg, TimeSpan now)
    {
        if (_revision is not { } revision)
        {
            _buffered.Add(msg);
            return new List<MemberEvent>();
        }

        if (msg.RoomRevision <= revision)
        {
            return new List<MemberEvent>();
        }

        _revision = msg.RoomRevision;
        Prune(now);
        var events = new List<MemberEvent>();
        _orphans.Remove(msg.PeerId);
        _tombstones[msg.PeerId] = Clock.Deadline(now, TombstoneTtl);
        if (_members.TryGetValue(msg.PeerId, out var member))
        {
            _members.Remove(msg.PeerId);
            events.Add(new MemberEvent.Left(member, msg.Mapping));
        }

        events.AddRange(SetCount(msg.ParticipantCount));
        return events;
    }

    public List<MemberEvent> ApplyUpdated(ParticipantState state, TimeSpan now)
    {
        Prune(now);
        if (_selfId == state.PeerId)
        {
            OwnState = state;
            return new List<MemberEvent> { new MemberEvent.Own(state) };
        }

        if (_tombstones.ContainsKey(state.PeerId))
        {
            return new List<MemberEvent>();
        }

        if (!_members.TryGetValue(state.PeerId, out var existing))
        {
            _orphans[state.PeerId] = (state, now);
            return new List<MemberEvent>();
        }

        return Upsert(existing with { State = state });
    }

    /// <summary>Records a slot/mid mapping learned elsewhere (<c>slot_assigned</c>).</summary>
    public void SetMapping(uint peerId, SlotMapping mapping)
    {
        if (_members.TryGetValue(peerId, out var member))
        {
            _members[peerId] = member with { Mapping = mapping };
        }
    }

    private List<MemberEvent> Upsert(MemberRecord record)
    {
        _members.TryGetValue(record.State.PeerId, out var previous);
        if (previous is not null && record.Mapping is null)
        {
            record = record with { Mapping = previous.Mapping };
        }

        _members[record.State.PeerId] = record;
        if (previous is null)
        {
            return new List<MemberEvent> { new MemberEvent.Joined(record) };
        }

        return previous.State != record.State
            ? new List<MemberEvent> { new MemberEvent.Updated(record, previous) }
            : new List<MemberEvent>();
    }

    private List<MemberEvent> SetCount(int count)
    {
        if (count == ParticipantCount)
        {
            return new List<MemberEvent>();
        }

        ParticipantCount = count;
        return new List<MemberEvent> { new MemberEvent.Count(count) };
    }

    private void Prune(TimeSpan now)
    {
        foreach (var id in _tombstones.Where(t => t.Value <= now).Select(t => t.Key).ToList())
        {
            _tombstones.Remove(id);
        }

        foreach (var id in _orphans.Where(o => now - o.Value.At > OrphanTtl).Select(o => o.Key).ToList())
        {
            _orphans.Remove(id);
        }
    }
}
