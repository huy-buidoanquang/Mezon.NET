using System.Net.Http.Headers;
using System.Text;
using Google.Protobuf;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport;

namespace Mezon.Net.Transport.Tests.Dev;

/// <summary>
/// Manual dev probe — run with: MEZON_DEV_PROBE=1 dotnet test --filter DevTransportProbe
/// </summary>
[Collection("DevTransport")]
public sealed class DevTransportProbe
{
    private const string DevApiHost = "dev-mezon.nccsoft.vn";
    private const string DevApiPort = "8088";
    private const string DevEmail = "pocolomos@gmail.com";
    private const string DevPassword = "C0nandoiner123$";

    [Fact]
    public async Task Probe_DevSocketEndpoints()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MEZON_DEV_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var options = new MezonSocketClientOptions(DevApiHost, DevApiPort, useSSL: true);
        var client = new MezonClient(options);
        var session = await client.AuthenticateEmailAsync(CreateAuthRequest()).ConfigureAwait(false);

        Assert.False(string.IsNullOrEmpty(session.SessionId), "SessionId required");
        Assert.False(string.IsNullOrEmpty(session.AuthToken), "AuthToken required");

        var tokensToTry = new[] { session.SessionId, session.AuthToken };
        var endpoints = new (string host, int port, bool ssl, TransportType type)[]
        {
            (MezonNetworkSettings.DefaultSocketHost, MezonNetworkSettings.DefaultSocketPort, true, TransportType.WebSocket),
            (MezonNetworkSettings.DefaultSocketHost, MezonNetworkSettings.DefaultSocketPort, true, TransportType.Tcp),
            ("dev-mezon.nccsoft.vn", 7349, true, TransportType.WebSocket),
            ("dev-mezon.nccsoft.vn", 7349, true, TransportType.Tcp),
        };

        var results = new StringBuilder();
        foreach (var endpoint in endpoints)
        {
            foreach (var token in tokensToTry)
            {
                var label = $"{endpoint.type} {endpoint.host}:{endpoint.port} token={(token == session.SessionId ? "SessionId" : "AuthToken")}";
                try
                {
                    await ProbeEndpointAsync(endpoint.host, endpoint.port, endpoint.ssl, endpoint.type, token).ConfigureAwait(false);
                    results.AppendLine($"OK  {label}");
                }
                catch (Exception ex)
                {
                    results.AppendLine($"FAIL {label}: {ex.GetType().Name} {ex.Message}");
                }
            }
        }

        Assert.Contains("OK  WebSocket dev-mezon-sock.nccsoft.vn:4433 token=SessionId", results.ToString(), StringComparison.Ordinal);
        Assert.Contains("OK  Tcp dev-mezon-sock.nccsoft.vn:4433 token=SessionId", results.ToString(), StringComparison.Ordinal);
    }

    private static async Task ProbeEndpointAsync(string host, int port, bool useSsl, TransportType type, string token)
    {
        IMezonNetworkTransporter transporter = type == TransportType.Tcp
            ? new MezonNetworkTcpTransporter()
            : new MezonNetworkWebSocketTransporter();

        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        transporter.Opened = () =>
        {
            opened.TrySetResult();
            return Task.CompletedTask;
        };
        transporter.ErrorOccurred = ex =>
        {
            error.TrySetResult(ex);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        transporter.SetCancelToken(cts.Token);

        var connectTask = transporter.ConnectAsync(host, port, token, useSsl, createStatus: false);
        var completed = await Task.WhenAny(connectTask, opened.Task, error.Task).ConfigureAwait(false);
        if (completed == error.Task)
        {
            throw await error.Task.ConfigureAwait(false);
        }

        await connectTask.ConfigureAwait(false);
        await transporter.DisconnectAsync().ConfigureAwait(false);
        if (transporter is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            transporter.Dispose();
        }
    }

    private static EmailAuthenticationRequest CreateAuthRequest() =>
        new()
        {
            Account = new AccountEmailRequest
            {
                Email = DevEmail,
                Password = DevPassword,
            },
            Create = false,
        };
}
