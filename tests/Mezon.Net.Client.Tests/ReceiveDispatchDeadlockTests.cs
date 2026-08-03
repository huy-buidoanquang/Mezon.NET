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

    [Fact]
    public async Task VoiceJoined_handler_does_not_block_receive_loop_from_reading_heartbeat_pong()
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

        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.VoiceJoinedEvent += async _ =>
        {
            handlerEntered.TrySetResult();
            await releaseHandler.Task.ConfigureAwait(false);
        };

        await client.ConnectAsync();

        transport.InjectRealtime(new Envelope
        {
            VoiceJoinedEvent = new VoiceJoinedEvent
            {
                ClanId = 1,
                VoiceChannelId = 2,
                UserId = 3,
            }
        });

        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // While the voice handler is still blocked, a heartbeat pong must still be completable
        // (receive loop must not be stuck awaiting TimedInvoke on the voice event).
        var heartbeat = socketClient.Heartbeat(new RequestOptions { SocketSendTimeout = 2_000 });
        var completed = await Task.WhenAny(heartbeat, Task.Delay(3_000));
        Assert.Same(heartbeat, completed);
        await heartbeat;

        releaseHandler.TrySetResult();
        await client.DisconnectAsync();
    }
}
