using Mezon.Net.Abstractions;
using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Tests;

public sealed class MezonApiExceptionTests
{
    [Theory]
    [InlineData(403, MezonStatusCode.Unknown)]
    [InlineData(503, MezonStatusCode.Unknown)]
    [InlineData(7, MezonStatusCode.PermissionDenied)]
    public void FromSocketResponse_keeps_the_code_the_server_sent(int code, MezonStatusCode expected)
    {
        var ex = MezonApiException.FromSocketResponse(code, "GetStreamPlayback", "Permission denied."u8.ToArray());

        Assert.Equal(expected, ex.StatusCode);
        Assert.Equal(code, ex.RawCode);
        Assert.Equal("Permission denied.", ex.Detail);
    }

    [Fact]
    public void Constructed_exception_uses_its_status_code_as_raw_code()
    {
        Assert.Equal((int)MezonStatusCode.NotFound, new MezonApiException(MezonStatusCode.NotFound).RawCode);
    }

    [Theory]
    [InlineData(403, 403)]
    [InlineData(0, (int)MezonStatusCode.Unknown)]
    public async Task Socket_api_answered_with_an_error_envelope_throws_its_code(int sent, int expected)
    {
        var transport = new LoopbackNetworkTransporter
        {
            ErrorResponder = envelope => envelope.ApiRequestEvent?.ApiName == "GetStreamPlayback"
                ? new Error { Code = sent, Message = "Permission denied." }
                : null,
        };
        var socketClient = await ConnectAsync(transport);

        var ex = await Assert.ThrowsAsync<MezonApiException>(() => socketClient.SendApiAsync(
            "GetStreamPlayback", new GetStreamPlaybackRequest { ChannelId = 42, Publish = true }, GetStreamPlaybackResponse.Parser));

        Assert.Equal(expected, ex.RawCode);
        Assert.Equal("Permission denied.", ex.Detail);
        await socketClient.DisconnectAsync();
    }

    [Fact]
    public async Task Realtime_send_answered_with_an_error_envelope_throws_its_code()
    {
        var transport = new LoopbackNetworkTransporter
        {
            ErrorResponder = envelope => envelope.MessageCase == Envelope.MessageOneofCase.ChannelMessageSend
                ? new Error { Code = 429, Message = "Rate limit exceeded" }
                : null,
        };
        var socketClient = await ConnectAsync(transport);

        var ex = await Assert.ThrowsAsync<MezonApiException>(() => socketClient.SendRtAwaitAckAsync(
            new Envelope { ChannelMessageSend = new ChannelMessageSend { ChannelId = 2 } }));

        Assert.Equal(429, ex.RawCode);
        await socketClient.DisconnectAsync();
    }

    private static async Task<MezonSocketClient> ConnectAsync(LoopbackNetworkTransporter transport)
    {
        var options = new MezonSocketClientOptions
        {
            RestClientProvider = _ => new FakeRestClient(),
            HeartbeatIntervalInMilliseconds = 60_000,
            ConnectionTimeoutInMilliseconds = 5_000,
            SocketTimeoutInMilliseconds = 2_000,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };
        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        await socketClient.ConnectAsync();
        return socketClient;
    }
}
