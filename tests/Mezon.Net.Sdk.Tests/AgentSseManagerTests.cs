using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Agent;
using Xunit;

namespace Mezon.Net.Sdk.Tests;

public sealed class AgentSseManagerTests
{
    [Fact]
    public async Task Manager_dispatches_sse_event_name_when_payload_has_no_event_type()
    {
        using var http = new HttpClient(new SingleResponseHandler(
            "event: room_started\n" +
            "data: {\"room\":{\"room_id\":\"room-1\"}}\n\n"))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var manager = new AgentSseManager(
            "http://agent.example",
            42,
            "token",
            http,
            maxReconnectAttempts: 1,
            reconnectDelayMs: 10);
        var received = new TaskCompletionSource<AgentSseSessionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.MessageReceived += evt =>
        {
            received.TrySetResult(evt);
            return Task.CompletedTask;
        };

        await manager.ConnectAsync();
        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(received.Task, completed);
        var result = await received.Task;
        Assert.Equal("room_started", result.EventType);
        Assert.Equal("{\"room\":{\"room_id\":\"room-1\"}}", result.RawResponse);
    }

    private sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;

        public SingleResponseHandler(string payload)
        {
            _payload = Encoding.UTF8.GetBytes(payload);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_payload)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream") }
                }
            });
        }
    }
}
