using System.Threading.Channels;
using Google.Protobuf;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Client.Tests.Helpers;

/// <summary>
/// Transport that mirrors production receive semantics: inbound frames are processed one-at-a-time
/// via <see cref="MessageReceived"/>, and API sends enqueue responses onto the same receive queue.
/// If <see cref="MessageReceived"/> awaits a socket API, the response cannot be delivered until the
/// callback returns — reproducing self-deadlock unless Client detaches dispatch from receive.
/// </summary>
internal sealed class LoopbackNetworkTransporter : IMezonNetworkTransporter
{
    private readonly System.Threading.Channels.Channel<(MezonMessageType Type, int Cid, int Code, byte[] Payload)> _inbound =
        System.Threading.Channels.Channel.CreateUnbounded<(MezonMessageType, int, int, byte[])>();

    private CancellationTokenSource? _loopCts;
    private Task? _receiveLoopTask;
    private int _connectCount;

    public int ConnectCount => _connectCount;

    public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
    public Func<Task>? Opened { get; set; }
    public Func<Exception?, Task>? Closed { get; set; }
    public Func<Exception, Task>? ErrorOccurred { get; set; }

    public void SetHeader(IDictionary<string, string> headers)
    {
    }

    public void SetCancelToken(CancellationToken cancellationToken)
    {
    }

    public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
    {
        Interlocked.Increment(ref _connectCount);
        _loopCts = new CancellationTokenSource();
        _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_loopCts.Token));
        if (Opened != null)
        {
            await Opened.Invoke().ConfigureAwait(false);
        }
    }

    public async Task DisconnectAsync(int closeCode = 1000, string? reason = null)
    {
        _loopCts?.Cancel();
        _inbound.Writer.TryComplete();
        if (_receiveLoopTask != null)
        {
            try
            {
                await _receiveLoopTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (Closed != null)
        {
            await Closed.Invoke(null).ConfigureAwait(false);
        }
    }

    public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
    {
        if (type == MezonMessageType.Heartbeat)
        {
            _inbound.Writer.TryWrite((MezonMessageType.Heartbeat, cid, 0, Array.Empty<byte>()));
            return default;
        }

        if (type is MezonMessageType.Api or MezonMessageType.Realtime)
        {
            // Respond with an empty ChannelDescription body for any API request (cid from envelope).
            try
            {
                var envelope = Envelope.Parser.ParseFrom(data.Span);
                if (envelope.MessageCase == Envelope.MessageOneofCase.ApiRequestEvent && envelope.Cid > 0)
                {
                    var body = new global::Mezon.Net.Internal.Api.ChannelDescription { ChannelId = 1, ClanId = 1 }.ToByteArray();
                    _inbound.Writer.TryWrite((MezonMessageType.Api, envelope.Cid, 0, body));
                }
            }
            catch
            {
            }
        }

        return default;
    }

    public void InjectRealtime(Envelope envelope)
    {
        _inbound.Writer.TryWrite((MezonMessageType.Realtime, 0, 0, envelope.ToByteArray()));
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _inbound.Writer.TryComplete();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var (type, cid, code, payload) in _inbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (MessageReceived != null)
                {
                    await MessageReceived.Invoke(type, cid, code, payload).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
