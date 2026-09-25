using System;
using System.Text;
using Mezon.Net.Sdk.Agent;
using Xunit;

namespace Mezon.Net.Sdk.Tests;

public class AgentSseTests
{
    [Fact]
    public void Endpoint_matches_mezon_sdk_query_authentication()
    {
        var uri = AgentSseManager.BuildEndpoint("http://agent.example/", 42, "tok en");
        Assert.Equal("http://agent.example/api/sse/metadata?appid=42&token=tok%20en", uri.AbsoluteUri);
    }

    [Fact]
    public void Decoder_reads_event_type_and_joins_data_lines()
    {
        using var decoder = new AgentSseDecoder();
        string? raw = null;
        string? eventType = null;
        var payload = Encoding.UTF8.GetBytes(": keep-alive\n\ndata: {\"event_type\":\"room_started\",\ndata: \"room\":{\"room_id\":\"r1\"}}\n\n");
        decoder.Push(payload, bytes =>
        {
            raw = Encoding.UTF8.GetString(bytes.Span);
            Span<char> type = stackalloc char[32];
            Assert.True(AgentSseDecoder.TryReadEventType(bytes.Span, type, out var written));
            eventType = new string(type.Slice(0, written));
        });

        Assert.Equal("room_started", eventType);
        Assert.Equal("{\"event_type\":\"room_started\",\n\"room\":{\"room_id\":\"r1\"}}", raw);
    }

    [Fact]
    public void Reconnect_delay_matches_sdk_backoff_cap()
    {
        Assert.Equal(3000, AgentSseManager.CalculateReconnectDelayMs(0, 3000, 0));
        Assert.Equal(6000, AgentSseManager.CalculateReconnectDelayMs(1, 3000, 0));
        Assert.Equal(30000, AgentSseManager.CalculateReconnectDelayMs(8, 3000, 0));
    }
}
