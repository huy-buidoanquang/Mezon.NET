using System.Collections.Concurrent;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;

namespace Mezon.Net.Transport.Tests.Helpers;

internal sealed class TransporterEventCapture
{
    private readonly ConcurrentQueue<(MezonMessageType type, int cid, int code, byte[] payload)> _messages = new();
    private int _openedCount;
    private int _closedCount;
    private int _errorCount;

    public int OpenedCount => _openedCount;
    public int ClosedCount => _closedCount;
    public int ErrorCount => _errorCount;
    public Exception? LastError { get; private set; }
    public Exception? LastClosedReason { get; private set; }

    public void Attach(IMezonNetworkTransporter transporter)
    {
        transporter.Opened = () =>
        {
            Interlocked.Increment(ref _openedCount);
            return Task.CompletedTask;
        };
        transporter.Closed = ex =>
        {
            Interlocked.Increment(ref _closedCount);
            LastClosedReason = ex;
            return Task.CompletedTask;
        };
        transporter.ErrorOccurred = ex =>
        {
            Interlocked.Increment(ref _errorCount);
            LastError = ex;
            return Task.CompletedTask;
        };
        transporter.MessageReceived = (type, cid, code, data) =>
        {
            _messages.Enqueue((type, cid, code, data.ToArray()));
            return default;
        };
    }

    public async Task<(MezonMessageType type, int cid, int code, byte[] payload)> WaitForMessageAsync(
        Func<(MezonMessageType type, int cid), bool>? predicate = null,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = Environment.TickCount64 + (long)timeout.Value.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (_messages.TryDequeue(out var message))
            {
                if (predicate == null || predicate((message.type, message.cid)))
                {
                    return message;
                }
            }
            else
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        throw new TimeoutException("Timed out waiting for transport message.");
    }

    public IReadOnlyList<(MezonMessageType type, int cid, int code, byte[] payload)> SnapshotMessages()
        => _messages.ToArray();
}
