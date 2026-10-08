using System.Collections.Concurrent;
using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Logging;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Realtime;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Client.Tests.Helpers;

internal sealed class FakeNetworkTransporter : IMezonNetworkTransporter
{
    private readonly ConcurrentDictionary<int, byte> _pendingHeartbeats = new();
    private CancellationToken _cancelToken;
    private bool _isConnected;

    public Func<Task>? ConnectHandler { get; set; }
    public bool AutoRespondToHeartbeat { get; set; } = true;
    public bool InvokeClosedDuringDisconnect { get; set; }

    /// <summary>Delay before a heartbeat is answered, so tests can observe a non-zero round trip.</summary>
    public int PongDelayMilliseconds { get; set; }

    private int _heartbeatSendCount;
    private int _connectCount;
    private int _disconnectCount;
    private int _closedInvokeCount;

    public int ConnectCount => _connectCount;
    public int DisconnectCount => _disconnectCount;
    public int HeartbeatSendCount => _heartbeatSendCount;
    public int ClosedInvokeCount => _closedInvokeCount;

    /// <summary>Token last passed to <see cref="SetCancelToken"/> (the connect token).</summary>
    public CancellationToken CancelToken => _cancelToken;

    public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
    public Func<Task>? Opened { get; set; }
    public Func<Exception?, Task>? Closed { get; set; }
    public Func<Exception, Task>? ErrorOccurred { get; set; }

    public void SetHeader(IDictionary<string, string> headers)
    {
    }

    public void SetCancelToken(CancellationToken cancellationToken) => _cancelToken = cancellationToken;

    /// <summary>Token passed to every ConnectAsync call, in order.</summary>
    public ConcurrentQueue<string?> ConnectTokens { get; } = new();

    public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
    {
        ConnectTokens.Enqueue(token);
        if (ConnectHandler != null)
        {
            await ConnectHandler().ConfigureAwait(false);
        }

        Interlocked.Increment(ref _connectCount);
        _isConnected = true;
        if (Opened != null)
        {
            await Opened.Invoke().ConfigureAwait(false);
        }
    }

    public async Task DisconnectAsync(int closeCode = 1000, string? reason = null)
    {
        if (!_isConnected)
        {
            return;
        }

        Interlocked.Increment(ref _disconnectCount);
        _isConnected = false;

        if (InvokeClosedDuringDisconnect && Closed != null)
        {
            Interlocked.Increment(ref _closedInvokeCount);
            await Closed.Invoke(null).ConfigureAwait(false);
        }
    }

    public async ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
    {
        if (!_isConnected)
        {
            return;
        }

        if (type == MezonMessageType.Heartbeat)
        {
            _pendingHeartbeats[cid] = 0;
            await AnswerHeartbeatAsync(MezonMessageType.Heartbeat, cid, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
        }
        else if (type == MezonMessageType.Realtime && Envelope.Parser.ParseFrom(data.Span) is { MessageCase: Envelope.MessageOneofCase.Ping } ping)
        {
            // A WebSocket heartbeat is a Ping envelope; MezonNetworkWebSocketTransporter delivers the Pong with cid 0.
            var pong = new Envelope { Cid = ping.Cid, Pong = new Pong() };
            await AnswerHeartbeatAsync(MezonMessageType.Realtime, 0, pong.ToByteArray()).ConfigureAwait(false);
        }
    }

    private async ValueTask AnswerHeartbeatAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> pong)
    {
        Interlocked.Increment(ref _heartbeatSendCount);
        if (!AutoRespondToHeartbeat || MessageReceived == null)
        {
            return;
        }

        if (PongDelayMilliseconds > 0)
        {
            await Task.Delay(PongDelayMilliseconds).ConfigureAwait(false);
        }

        await MessageReceived.Invoke(type, cid, 0, pong).ConfigureAwait(false);
    }

    public void TriggerClosed(Exception? exception = null)
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;
        Interlocked.Increment(ref _closedInvokeCount);
        if (Closed != null)
        {
            _ = Closed.Invoke(exception);
        }
    }

    public void TriggerClosedSynchronously(Exception? exception = null)
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;
        Interlocked.Increment(ref _closedInvokeCount);
        Closed?.Invoke(exception).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
    }
}

internal static class SocketTestDoubles
{
    public static MezonSocketClientOptions CreateOptions(FakeNetworkTransporter transport, int heartbeatMs = 150, int connectionTimeoutMs = 5000, TransportType transportType = TransportType.Tcp)
    {
        return new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = heartbeatMs,
            ConnectionTimeoutInMilliseconds = connectionTimeoutMs,
            TransportType = transportType,
            NetworkTransportProvider = _ => transport,
        };
    }

    public static async Task<MezonSocketClient> CreateLoggedInSocketClientAsync(MezonSocketClientOptions options, IMezonNetworkTransporter transport)
    {
        var logManager = new LogManager(LogLevel.Error);
        var sessionManager = new SessionManager<MezonApiClientOptions>(options, logManager);
        var socketClient = new MezonSocketClient(options.RestClientProvider, _ => transport, options);
        socketClient.ConfigureSessionAccessor(() => sessionManager.CurrentSession());
        await sessionManager.LoginAsync(new TestSession("session-token", "127.0.0.1:9000")).ConfigureAwait(false);

        typeof(MezonApiClient).GetProperty(nameof(MezonApiClient.LoginState))!
            .SetValue(socketClient, LoginState.LoggedIn);
        return socketClient;
    }

    public static void SetReconnectDelay(MezonClient client, int delayMs) => client.SetReconnectDelayForTests(delayMs);
}
