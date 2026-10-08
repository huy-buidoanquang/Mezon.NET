using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.Client.Tests;

public sealed class RealtimeFrameDecodeTests
{
    [Fact]
    public async Task Undecodable_realtime_frame_is_reported_as_warning()
    {
        var transport = new LoopbackNetworkTransporter();
        var options = new MezonSocketClientOptions
        {
            HeartbeatIntervalInMilliseconds = 60_000,
            ConnectionTimeoutInMilliseconds = 5_000,
            SocketTimeoutInMilliseconds = 2_000,
            TransportType = TransportType.Tcp,
            NetworkTransportProvider = _ => transport,
        };

        var socketClient = await SocketTestDoubles.CreateLoggedInSocketClientAsync(options, transport);
        var client = new MezonClient(options, socketClient);
        var warned = new TaskCompletionSource<LogMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Log += message =>
        {
            if (message.Level == LogLevel.Warning && message.Message.Contains("undecodable"))
            {
                warned.TrySetResult(message);
            }

            return Task.CompletedTask;
        };

        await client.ConnectAsync();

        // Envelope{cid=88} followed by a truncated length-delimited field.
        transport.InjectRawRealtime([0x08, 0x58, 0x22, 0x05, 0x01]);

        var log = await warned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("bytes=5", log.Message);
        Assert.Equal(1, socketClient.UndecodableFrameCount);

        await client.DisconnectAsync();
    }
}
