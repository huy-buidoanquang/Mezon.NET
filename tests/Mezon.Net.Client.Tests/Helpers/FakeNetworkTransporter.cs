using System.Collections.Concurrent;
using Mezon.Net.Client;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Logging;
using Mezon.Net.Core.Abstractions;
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

    private int _heartbeatSendCount;
    private int _connectCount;
    private int _disconnectCount;
    private int _closedInvokeCount;

    public int ConnectCount => _connectCount;
    public int DisconnectCount => _disconnectCount;
    public int HeartbeatSendCount => _heartbeatSendCount;
    public int ClosedInvokeCount => _closedInvokeCount;

    public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
    public Func<Task>? Opened { get; set; }
    public Func<Exception?, Task>? Closed { get; set; }
    public Func<Exception, Task>? ErrorOccurred { get; set; }

    public void SetHeader(IDictionary<string, string> headers)
    {
    }

    public void SetCancelToken(CancellationToken cancellationToken) => _cancelToken = cancellationToken;

    public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
    {
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
            Interlocked.Increment(ref _heartbeatSendCount);
            _pendingHeartbeats[cid] = 0;
            if (AutoRespondToHeartbeat && MessageReceived != null)
            {
                await MessageReceived.Invoke(MezonMessageType.Heartbeat, cid, 0, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
            }
        }
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
    public static MezonSocketClientOptions CreateOptions(FakeNetworkTransporter transport, int heartbeatMs = 150, int connectionTimeoutMs = 5000)
    {
        return new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = heartbeatMs,
            ConnectionTimeoutInMilliseconds = connectionTimeoutMs,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };
    }

    public static async Task<MezonSocketClient> CreateLoggedInSocketClientAsync(MezonSocketClientOptions options, FakeNetworkTransporter transport)
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
