using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Transport.Tests.Helpers;

namespace Mezon.Net.Transport.Tests.Dev;

[Collection("DevTransport")]
public sealed class DevTransportIntegrationTests
{
    private static readonly SemaphoreSlim DevSessionGate = new(1, 1);
    private const string DevApiHost = "dev-mezon.nccsoft.vn";
    private const string DevApiPort = "8088";
    private const string DevSocketHost = "dev-mezon-sock.nccsoft.vn";
    private const int DevSocketPort = 4433;

    public static IEnumerable<object[]> DevTransporters() =>
    [
        [TransporterKind.Tcp],
        [TransporterKind.WebSocket],
    ];

    internal static (MezonMessageType Type, ReadOnlyMemory<byte> Payload) CreateHeartbeatFrame(TransporterKind kind, int cid) => kind switch
    {
        TransporterKind.Tcp => (MezonMessageType.Heartbeat, ReadOnlyMemory<byte>.Empty),
        TransporterKind.WebSocket => (MezonMessageType.Realtime, new Envelope { Cid = cid, Ping = new Ping() }.ToByteArray()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported transporter kind."),
    };

    internal static bool IsHeartbeatResponse(
        TransporterKind kind,
        int cid,
        MezonMessageType type,
        int responseCid,
        ReadOnlySpan<byte> payload) => kind switch
    {
        TransporterKind.Tcp => type == MezonMessageType.Heartbeat && responseCid == cid,
        TransporterKind.WebSocket => type == MezonMessageType.Realtime && IsMatchingPong(payload, cid),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported transporter kind."),
    };

    private static bool IsMatchingPong(ReadOnlySpan<byte> payload, int cid)
    {
        try
        {
            var envelope = Envelope.Parser.ParseFrom(payload);
            return envelope.MessageCase == Envelope.MessageOneofCase.Pong && envelope.Cid == cid;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    internal static async Task SafeCloseAndDisposeAsync(IMezonNetworkTransporter transporter)
    {
        try
        {
            await transporter.DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        await TransporterFactory.DisposeAsync(transporter).ConfigureAwait(false);
    }

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

            try
            {
                await transporter.ConnectAsync(
                    DevSocketHost,
                    DevSocketPort,
                    token,
                    useSsl: true,
                    createStatus: false).ConfigureAwait(false);

                Assert.Equal(1, events.OpenedCount);
                await transporter.DisconnectAsync().ConfigureAwait(false);
                Assert.Equal(1, events.ClosedCount);
            }
            finally
            {
                await SafeCloseAndDisposeAsync(transporter).ConfigureAwait(false);
            }
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

            try
            {
                await transporter.ConnectAsync(
                    DevSocketHost,
                    DevSocketPort,
                    token,
                    useSsl: true,
                    createStatus: false).ConfigureAwait(false);

                var (messageType, payload) = CreateHeartbeatFrame(kind, 1);
                await transporter.SendAsync(messageType, 1, payload).ConfigureAwait(false);
                var deadline = Environment.TickCount64 + 5_000;
                var matched = false;
                while (!matched && Environment.TickCount64 < deadline)
                {
                    var remaining = TimeSpan.FromMilliseconds(Math.Max(100, deadline - Environment.TickCount64));
                    var frame = await events.WaitForMessageAsync(null, remaining).ConfigureAwait(false);
                    matched = IsHeartbeatResponse(kind, 1, frame.type, frame.cid, frame.payload);
                }

                Assert.True(matched);
            }
            finally
            {
                await SafeCloseAndDisposeAsync(transporter).ConfigureAwait(false);
            }
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

            try
            {
                await transporter.ConnectAsync(
                    DevSocketHost,
                    DevSocketPort,
                    auth.SessionId,
                    useSsl: true).ConfigureAwait(false);

                Assert.Equal(1, events.OpenedCount);
            }
            finally
            {
                await SafeCloseAndDisposeAsync(transporter).ConfigureAwait(false);
            }
        });
    }

    private static bool DevTestsEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("MEZON_RUN_DEV_TESTS"), "1", StringComparison.Ordinal);

    private static async Task<string> AuthenticateAsync()
    {
        var auth = await AuthenticateRawAsync().ConfigureAwait(false);
        return auth.SessionId;
    }

    private static async Task<Session> AuthenticateRawAsync()
    {
        var serverKey = Environment.GetEnvironmentVariable("MEZON_SERVER_KEY");
        var options = new MezonSocketClientOptions(DevApiHost, DevApiPort, useSSL: true)
        {
            ServerKey = string.IsNullOrWhiteSpace(serverKey) ? "defaultkey" : serverKey,
        };
        await using var client = new MezonClient(options);
        return await client.AuthenticateEmailAsync(CreateAuthRequest()).ConfigureAwait(false);
    }

    private static EmailAuthenticationRequest CreateAuthRequest()
    {
        var email = Environment.GetEnvironmentVariable("MEZON_EMAIL");
        var password = Environment.GetEnvironmentVariable("MEZON_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set MEZON_EMAIL and MEZON_PASSWORD before running MEZON_RUN_DEV_TESTS=1.");
        }

        return new EmailAuthenticationRequest
        {
            Account = new AccountEmailRequest
            {
                Email = email,
                Password = password,
            },
            Create = false,
        };
    }
}
