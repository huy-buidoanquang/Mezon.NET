using System.Reflection;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Client;
using Mezon.Net.Logging;
using Mezon.Net.Client.Tests.Helpers;
using Xunit;

namespace Mezon.Net.Client.Tests;

public sealed class SessionManagerIsolationTests
{
    [Fact]
    public async Task Two_mezon_clients_do_not_share_session_manager_state()
    {
        var logManager = new LogManager(LogLevel.Error);
        var options = new MezonSocketClientOptions();
        var sessionManager1 = new SessionManager<MezonApiClientOptions>(options, logManager);
        var sessionManager2 = new SessionManager<MezonApiClientOptions>(options, logManager);

        await sessionManager1.LoginAsync(new TestSession("token-one", "127.0.0.1:9000")).ConfigureAwait(false);
        await sessionManager2.LoginAsync(new TestSession("token-two", "127.0.0.1:9001")).ConfigureAwait(false);

        Assert.NotSame(sessionManager1, sessionManager2);
        Assert.Equal("token-one", sessionManager1.CurrentSession().AuthToken);
        Assert.Equal("token-two", sessionManager2.CurrentSession().AuthToken);
    }

    [Fact]
    public async Task BaseMezonClient_instances_use_distinct_session_managers()
    {
        var client1 = new MezonClient(new MezonSocketClientOptions());
        var client2 = new MezonClient(new MezonSocketClientOptions());
        var sessionManager1 = GetSessionManager(client1);
        var sessionManager2 = GetSessionManager(client2);

        Assert.NotSame(sessionManager1, sessionManager2);

        await sessionManager1.LoginAsync(new TestSession("client-one", "127.0.0.1:9000")).ConfigureAwait(false);
        await sessionManager2.LoginAsync(new TestSession("client-two", "127.0.0.1:9001")).ConfigureAwait(false);

        Assert.Equal("client-one", client1.CurrentSession.AuthToken);
        Assert.Equal("client-two", client2.CurrentSession.AuthToken);
    }

    private static SessionManager<MezonApiClientOptions> GetSessionManager(MezonClient client)
    {
        var property = typeof(BaseMezonClient).GetProperty("SessionManager", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(BaseMezonClient).FullName, "SessionManager");
        return (SessionManager<MezonApiClientOptions>)property.GetValue(client)!;
    }
}
