using System.Collections.Concurrent;
using Mezon.Net.Api;
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
        var options = new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = heartbeatMs,
            ConnectionTimeoutInMilliseconds = connectionTimeoutMs,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };
        SessionManager<MezonApiClientOptions>.GetOrCreate(options, new LogManager(LogLevel.Error));
        return options;
    }

    public static async Task<MezonSocketApiClient> CreateLoggedInSocketClientAsync(MezonSocketClientOptions options, FakeNetworkTransporter transport)
    {
        var socketClient = new MezonSocketApiClient(options.RestClientProvider, _ => transport, options);
        await SessionManager<MezonApiClientOptions>.Instance.LoginAsync(new TestSession("session-token", "127.0.0.1:9000")).ConfigureAwait(false);

        typeof(MezonApiClient).GetProperty(nameof(MezonApiClient.LoginState))!
            .SetValue(socketClient, LoginState.LoggedIn);
        return socketClient;
    }

    public static void SetReconnectDelay(MezonClient client, int delayMs) => client.SetReconnectDelayForTests(delayMs);

    private sealed class TestSession : Mezon.Net.Abstractions.ISession
    {
        public TestSession(string sessionId, string tcpUrl)
        {
            SessionId = sessionId;
            AuthToken = sessionId;
            TcpUrl = tcpUrl;
            WsUrl = tcpUrl;
        }

        public string SessionId { get; }
        public string AuthToken { get; }
        public string RefreshToken => AuthToken;
        public bool Created => true;
        public long CreatedAt => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public long ExpiresAt => DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        public long RefreshExpiresAt => ExpiresAt;
        public string? Username => "test";
        public string? UserId => "1";
        public bool IsRemember => false;
        public string? ApiUrl => "http://127.0.0.1:8088";
        public string? WsUrl { get; }
        public string? TcpUrl { get; }

        public bool IsExpiredSoon(int seconds) => false;
        public bool IsRefreshExpiredSoon(int seconds) => false;
        public bool IsExpired() => false;
        public bool IsRefreshExpired() => false;
    }
}
