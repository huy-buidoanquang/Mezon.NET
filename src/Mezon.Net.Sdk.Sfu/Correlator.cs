using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Sfu.Core;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>What an acknowledgement carries back to the caller.</summary>
internal abstract record Ack
{
    public sealed record Done : Ack;

    public sealed record Moderation(string Action, string UserId, int Affected) : Ack;

    public sealed record Visibility(bool Camera, bool Screen) : Ack;

    public static readonly Ack Ok = new Done();
}

/// <summary>
/// Pairs requests with server replies. The server answers inside the handler but its replies carry no request id:
/// an acknowledgement settles the oldest pending operation whose matcher accepts it, and an error rejects the
/// oldest pending operation that can produce that error code. Not thread-safe: used under the room lock.
/// </summary>
internal sealed class Correlator
{
    private sealed record Pending(long Id, string Operation, Func<ServerMessage, Ack?> Matcher, TimeSpan Deadline, TaskCompletionSource<Ack> Reply);

    private readonly List<Pending> _pending = new();
    private long _nextId;

    public int Count => _pending.Count;

    public (long Id, Task<Ack> Reply) Track(string operation, Func<ServerMessage, Ack?> matcher, TimeSpan deadline)
    {
        var reply = new TaskCompletionSource<Ack>(TaskCreationOptions.RunContinuationsAsynchronously);
        _nextId++;
        _pending.Add(new Pending(_nextId, operation, matcher, deadline, reply));
        return (_nextId, reply.Task);
    }

    public void Remove(long id) => _pending.RemoveAll(p => p.Id == id);

    /// <summary>Offers a non-error server message; returns whether an operation was settled.</summary>
    public bool HandleMessage(ServerMessage message)
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Matcher(message) is { } ack)
            {
                var op = _pending[i];
                _pending.RemoveAt(i);
                op.Reply.TrySetResult(ack);
                return true;
            }
        }

        return false;
    }

    /// <summary>Attributes a server error to the oldest pending operation that can produce it; returns its name.</summary>
    public string? HandleError(string code)
    {
        if (ServerErrors.Find(code) is not { } info)
        {
            return null;
        }

        var index = _pending.FindIndex(p => info.Sources.Contains(p.Operation));
        if (index < 0)
        {
            return null;
        }

        var op = _pending[index];
        _pending.RemoveAt(index);
        op.Reply.TrySetException(SfuException.Signaling(code, op.Operation, info.Retryable));
        return op.Operation;
    }

    /// <summary>Rejects operations whose deadline passed and returns their names.</summary>
    public List<string> Expire(TimeSpan now)
    {
        var expired = _pending.Where(p => p.Deadline <= now).ToList();
        _pending.RemoveAll(p => p.Deadline <= now);
        foreach (var op in expired)
        {
            op.Reply.TrySetException(SfuException.Timeout(op.Operation));
        }

        return expired.Select(op => op.Operation).ToList();
    }

    public void RejectAll(SfuException error)
    {
        var all = _pending.ToList();
        _pending.Clear();
        foreach (var op in all)
        {
            op.Reply.TrySetException(error);
        }
    }
}
