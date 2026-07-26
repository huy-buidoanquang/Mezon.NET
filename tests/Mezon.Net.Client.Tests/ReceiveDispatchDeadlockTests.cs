using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;

namespace Mezon.Net.Client.Tests;

public sealed class ReceiveDispatchDeadlockTests
{
    [Fact]
    public async Task ChannelMessage_handler_can_await_socket_api_without_timeout()
    {
        var transport = new LoopbackNetworkTransporter();
        var options = new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = 60_000,
            ConnectionTimeoutInMilliseconds = 5_000,
            SocketTimeoutInMilliseconds = 2_000,
            SocketHandlerTimeoutInMilliseconds = 500,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };

        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var client = new MezonClient(options, socketClient);

        var detailReady = new TaskCompletionSource<ChannelDescriptionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ChannelMessageReceivedEvent += async _ =>
        {
            try
            {
                var detail = await client.GetChannelDetailAsync(channelId: 1);
                detailReady.TrySetResult(detail);
            }
            catch (Exception ex)
            {
                detailReady.TrySetException(ex);
            }
        };

        await client.ConnectAsync();

        transport.InjectRealtime(new Envelope
        {
            ChannelMessage = new ChannelMessage
            {
                MessageId = 1,
                ChannelId = 1,
                ClanId = 1,
                SenderId = 2,
                Content = "{}",
            }
        });

        var completed = await Task.WhenAny(detailReady.Task, Task.Delay(5_000));
        Assert.Same(detailReady.Task, completed);
        var detail = await detailReady.Task;
        Assert.Equal(1, detail.ChannelId);

        await client.DisconnectAsync();
    }
}
