using System.Reflection;
using Google.Protobuf;
using Mezon.Net.Abstractions;
using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.Client.Tests;

public sealed class SessionRefreshTests
{
    private const string AppAuthenticateEndpoint = "/v2/apps/authenticate/token";

    [Fact]
    public async Task Concurrent_refreshes_coalesce_into_one_reauthentication()
    {
        var rest = new FakeRestClient { ResponseDelayMs = 50 };
        var logins = 0;
        rest.Responder = (_, endpoint) => endpoint == AppAuthenticateEndpoint
            ? TestJwt.CreateSession(Interlocked.Increment(ref logins) == 1 ? TimeSpan.FromSeconds(10) : TimeSpan.FromHours(1), $"sid-{logins}").ToByteArray()
            : null;
        var sessions = CreateSessionManager(rest);
        await sessions.LoginAsync(42, "secret");

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => sessions.GetOrRefreshAsync())));

        Assert.Equal(2, rest.CallCount);
        Assert.Equal("sid-2", sessions.CurrentSession().SessionId);
        Assert.All(tokens, token => Assert.Equal(sessions.CurrentSession().AuthToken, token));
        Assert.Equal(1, GetSessionLock(sessions).CurrentCount);
    }

    [Fact]
    public async Task Refresh_goes_over_the_socket_when_connected()
    {
        var rest = new FakeRestClient
        {
            Responder = (_, endpoint) => endpoint == AppAuthenticateEndpoint
                ? TestJwt.CreateSession(TimeSpan.FromSeconds(10), "sid-login").ToByteArray()
                : null,
        };
        string? refreshApi = null;
        var transport = new LoopbackNetworkTransporter
        {
            ApiResponder = request =>
            {
                refreshApi = request.ApiName;
                return request.ApiName == "SessionRefresh"
                    ? TestJwt.CreateSession(TimeSpan.FromHours(1), "sid-socket").ToByteArray()
                    : null;
            },
        };
        var options = CreateOptions(rest, transport);
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var sessions = new SessionManager<MezonApiClientOptions>(options, new LogManager(LogLevel.Error));
        sessions.AttachSocket(socketClient, () => socketClient.ConnectionState == ConnectionState.Connected);
        await sessions.LoginAsync(42, "secret");
        await socketClient.ConnectAsync();

        await sessions.GetOrRefreshAsync();

        Assert.Equal("SessionRefresh", refreshApi);
        Assert.Equal("sid-socket", sessions.CurrentSession().SessionId);
        Assert.Equal(1, rest.CallCount);
        await socketClient.DisconnectAsync();
    }

    [Fact]
    public async Task Refresh_reauthenticates_when_socket_is_down()
    {
        var logins = 0;
        var rest = new FakeRestClient
        {
            Responder = (_, endpoint) => endpoint == AppAuthenticateEndpoint
                ? TestJwt.CreateSession(Interlocked.Increment(ref logins) == 1 ? TimeSpan.FromSeconds(10) : TimeSpan.FromHours(1), $"sid-{logins}").ToByteArray()
                : null,
        };
        var sessions = CreateSessionManager(rest);
        sessions.AttachSocket(new MezonApiClient(_ => rest, new MezonSocketClientOptions()), () => false);
        await sessions.LoginAsync(42, "secret");

        await sessions.GetOrRefreshAsync();

        Assert.Equal("sid-2", sessions.CurrentSession().SessionId);
        Assert.Equal(2, rest.CallCount);
    }

    [Fact]
    public async Task Failed_refresh_keeps_the_current_session()
    {
        var logins = 0;
        var rest = new FakeRestClient
        {
            Responder = (_, endpoint) => endpoint == AppAuthenticateEndpoint && Interlocked.Increment(ref logins) == 1
                ? TestJwt.CreateSession(TimeSpan.FromSeconds(10), "sid-1").ToByteArray()
                : null,
        };
        var sessions = CreateSessionManager(rest);
        await sessions.LoginAsync(42, "secret");
        var before = sessions.CurrentSession();

        await Assert.ThrowsAsync<SessionRefreshFailedException>(() => sessions.GetOrRefreshAsync());

        Assert.Same(before, sessions.CurrentSession());
    }

    [Fact]
    public async Task EnsureFresh_without_a_refresh_route_returns_false()
    {
        var sessions = CreateSessionManager(new FakeRestClient());
        await sessions.LoginAsync(new TestSession("token", "127.0.0.1:9000"));

        Assert.False(await sessions.EnsureFreshAsync(force: true));
        Assert.Equal("token", sessions.CurrentSession().AuthToken);
    }

    [Fact]
    public async Task Pushed_session_replaces_the_current_session()
    {
        var sessions = CreateSessionManager(new FakeRestClient());
        await sessions.LoginAsync(new TestSession("old", "127.0.0.1:9000"));
        ISession? raised = null;
        sessions.SessionRefreshed += session =>
        {
            raised = session;
            return Task.CompletedTask;
        };

        var pushed = new Session(TestJwt.CreateSession(TimeSpan.FromHours(1), "sid-pushed"));
        await sessions.ApplyPushedSessionAsync(pushed);

        Assert.Same(pushed, sessions.CurrentSession());
        Assert.Same(pushed, raised);
    }

    [Fact]
    public async Task Logout_without_a_socket_clears_the_session()
    {
        var sessions = CreateSessionManager(new FakeRestClient());
        await sessions.LoginAsync(new TestSession("token", "127.0.0.1:9000"));

        await sessions.LogoutAsync();

        Assert.Equal(string.Empty, sessions.CurrentSession().AuthToken);
    }

    [Fact]
    public async Task Reconnect_reauthenticates_an_expiring_bot_session_first()
    {
        var logins = 0;
        var rest = new FakeRestClient
        {
            Responder = (_, endpoint) => endpoint == AppAuthenticateEndpoint
                ? TestJwt.CreateSession(Interlocked.Increment(ref logins) == 1 ? TimeSpan.FromSeconds(10) : TimeSpan.FromHours(1), $"sid-{logins}").ToByteArray()
                : null,
        };
        var transport = new FakeNetworkTransporter();
        var options = SocketTestDoubles.CreateOptions(transport, heartbeatMs: 60_000);
        options.RestClientProvider = _ => rest;
        var client = new MezonClient(options);
        client.SetReconnectDelayForTests(50);

        Assert.True(await client.LoginAsBotInternalAsync(42, "secret", autoRefreshSession: true));
        await client.ConnectAsync();
        transport.TriggerClosed();

        var deadline = Environment.TickCount64 + 5000;
        while (transport.ConnectTokens.Count < 2 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(new string?[] { "sid-1", "sid-2" }, transport.ConnectTokens.Take(2));
        await client.DisconnectAsync();
    }

    private static MezonSocketClientOptions CreateOptions(FakeRestClient rest, LoopbackNetworkTransporter transport)
        => new()
        {
            RestClientProvider = _ => rest,
            HeartbeatIntervalInMilliseconds = 60_000,
            ConnectionTimeoutInMilliseconds = 5_000,
            SocketTimeoutInMilliseconds = 2_000,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };

    private static SessionManager<MezonApiClientOptions> CreateSessionManager(FakeRestClient rest)
        => new(new MezonSocketClientOptions { RestClientProvider = _ => rest }, new LogManager(LogLevel.Error));

    private static SemaphoreSlim GetSessionLock(SessionManager<MezonApiClientOptions> sessions)
        => (SemaphoreSlim)typeof(SessionManager<MezonApiClientOptions>)
            .GetField("_sessionLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(sessions)!;
}
