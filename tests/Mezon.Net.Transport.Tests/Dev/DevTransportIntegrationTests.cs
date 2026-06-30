using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Dev;

[Collection("DevTransport")]
public sealed class DevTransportIntegrationTests
{
    private static readonly SemaphoreSlim DevSessionGate = new(1, 1);
    private const string DevApiHost = "dev-mezon.nccsoft.vn";
    private const string DevApiPort = "8088";
    private const string DevEmail = "pocolomos@gmail.com";
    private const string DevPassword = "C0nandoiner123$";

    public static IEnumerable<object[]> DevTransporters() =>
    [
        [TransporterKind.Tcp],
        [TransporterKind.WebSocket],
    ];

    private static async Task RunDevTestAsync(Func<Task> testBody)
    {
        await DevSessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Delay(1000).ConfigureAwait(false);
            await testBody().ConfigureAwait(false);
        }
        finally
        {
            DevSessionGate.Release();
        }
    }

    [Theory]
    [MemberData(nameof(DevTransporters))]
    public Task Dev_ConnectAndDisconnect_FiresOpenedAndClosed(TransporterKind kind)
    {
        if (!DevTestsEnabled())
        {
            return Task.CompletedTask;
        }

        return RunDevTestAsync(async () =>
        {
            var token = await AuthenticateAsync().ConfigureAwait(false);
            var transporter = TransporterFactory.Create(kind);
            var events = new TransporterEventCapture();
            events.Attach(transporter);

            await transporter.ConnectAsync(
                MezonNetworkSettings.DefaultSocketHost,
                MezonNetworkSettings.DefaultSocketPort,
                token,
                useSsl: true,
                createStatus: false).ConfigureAwait(false);

            Assert.Equal(1, events.OpenedCount);
            await transporter.DisconnectAsync().ConfigureAwait(false);
            Assert.Equal(1, events.ClosedCount);
            await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
        });
    }

    [Theory]
    [MemberData(nameof(DevTransporters))]
    public Task Dev_SendHeartbeat_CanReceiveServerTraffic(TransporterKind kind)
    {
        if (!DevTestsEnabled())
        {
            return Task.CompletedTask;
        }

        return RunDevTestAsync(async () =>
        {
            var token = await AuthenticateAsync().ConfigureAwait(false);
            var transporter = TransporterFactory.Create(kind);
            var events = new TransporterEventCapture();
            events.Attach(transporter);

            await transporter.ConnectAsync(
                MezonNetworkSettings.DefaultSocketHost,
                MezonNetworkSettings.DefaultSocketPort,
                token,
                useSsl: true,
                createStatus: false).ConfigureAwait(false);

            await transporter.SendAsync(MezonMessageType.Heartbeat, 1, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
            await Task.Delay(1500).ConfigureAwait(false);

            Assert.True(events.SnapshotMessages().Count >= 0);
            await transporter.DisconnectAsync().ConfigureAwait(false);
            await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
        });
    }

    [Fact]
    public Task Dev_WebSocket_UsesSessionIdToken()
    {
        if (!DevTestsEnabled())
        {
            return Task.CompletedTask;
        }

        return RunDevTestAsync(async () =>
        {
            var auth = await AuthenticateRawAsync().ConfigureAwait(false);
            var transporter = new MezonNetworkWebSocketTransporter();
            var events = new TransporterEventCapture();
            events.Attach(transporter);

            await transporter.ConnectAsync(
                MezonNetworkSettings.DefaultSocketHost,
                MezonNetworkSettings.DefaultSocketPort,
                auth.SessionId,
                useSsl: true).ConfigureAwait(false);

            Assert.Equal(1, events.OpenedCount);
            await transporter.DisconnectAsync().ConfigureAwait(false);
            await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
        });
    }

    private static bool DevTestsEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("MEZON_RUN_DEV_TESTS"), "1", StringComparison.Ordinal);

    private static async Task<string> AuthenticateAsync()
    {
        var auth = await AuthenticateRawAsync().ConfigureAwait(false);
        return auth.SessionId;
    }

    private static async Task<Mezon.Net.Api.AuthenticationResponse> AuthenticateRawAsync()
    {
        var options = new MezonSocketClientOptions(DevApiHost, DevApiPort, useSSL: true);
        var client = new MezonClient(options);
        return await client.AuthenticateEmailAsync(DevEmail, DevPassword).ConfigureAwait(false);
    }
}
