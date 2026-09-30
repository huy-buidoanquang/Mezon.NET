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
    public void Decoder_reads_sse_event_type_and_joins_data_lines()
    {
        using var decoder = new AgentSseDecoder();
        string? raw = null;
        string? eventType = null;
        var payload = Encoding.UTF8.GetBytes(": keep-alive\n\nevent: room_started\ndata: {\"room\":{\ndata: \"room_id\":\"r1\"}}\n\n");
        decoder.Push(payload, (sseType, bytes) =>
        {
            eventType = Encoding.UTF8.GetString(sseType.Span);
            raw = Encoding.UTF8.GetString(bytes.Span);
        });

        Assert.Equal("room_started", eventType);
        Assert.Equal("{\"room\":{\n\"room_id\":\"r1\"}}", raw);
    }

    [Fact]
    public void Decoder_preserves_json_event_type_fallback_without_sse_event()
    {
        using var decoder = new AgentSseDecoder();
        ReadOnlyMemory<byte> sseType = default;
        string? raw = null;
        var payload = Encoding.UTF8.GetBytes("data: {\"event_type\":\"room_ended\"}\n\n");
        decoder.Push(payload, (eventType, data) =>
        {
            sseType = eventType;
            raw = Encoding.UTF8.GetString(data.Span);
        });

        Assert.Empty(sseType.ToArray());
        Assert.Equal("{\"event_type\":\"room_ended\"}", raw);
        Span<char> buffer = stackalloc char[32];
        Assert.True(AgentSseDecoder.TryReadEventType(Encoding.UTF8.GetBytes(raw!).AsSpan(), buffer, out var written));
        Assert.Equal("room_ended", new string(buffer[..written]));
    }

    [Fact]
    public void Reconnect_delay_matches_sdk_backoff_cap()
    {
        Assert.Equal(3000, AgentSseManager.CalculateReconnectDelayMs(0, 3000, 0));
        Assert.Equal(6000, AgentSseManager.CalculateReconnectDelayMs(1, 3000, 0));
        Assert.Equal(30000, AgentSseManager.CalculateReconnectDelayMs(8, 3000, 0));
    }
}
