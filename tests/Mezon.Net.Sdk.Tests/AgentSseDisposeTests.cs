using System.Net.Http;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Agent;
using Xunit;

namespace Mezon.Net.Sdk.Tests;

public sealed class AgentSseDisposeTests
{
    [Fact]
    public async Task Async_dispose_waits_for_reconnect_loop_to_stop()
    {
        using var http = new HttpClient(new ImmediateHttpMessageHandler());
        var manager = new AgentSseManager(
            "http://agent.example",
            42,
            "token",
            http,
            reconnectDelayMs: 1000);

        await manager.ConnectAsync();
        await manager.DisposeAsync();

        await manager.DisposeAsync();
    }
}
