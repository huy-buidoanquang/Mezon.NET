using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;

namespace Mezon.Net.Transport.Tests.Helpers;

public enum TransporterKind
{
    Tcp,
    WebSocket,
}

internal static class TransporterFactory
{
    public static IMezonNetworkTransporter Create(TransporterKind kind) => kind switch
    {
        TransporterKind.Tcp => new MezonNetworkTcpTransporter(),
        TransporterKind.WebSocket => new MezonNetworkWebSocketTransporter(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static async ValueTask DisposeAsync(IMezonNetworkTransporter transporter)
    {
        if (transporter is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            transporter.Dispose();
        }
    }
}

internal sealed class LoopbackSession : IAsyncDisposable
{
    public required TcpLoopbackServer? TcpServer { get; init; }
    public required WebSocketLoopbackServer? WebSocketServer { get; init; }
    public required int Port { get; init; }

    public static async Task<LoopbackSession> StartAsync(
        TransporterKind kind,
        Func<object, CancellationToken, Task> clientHandler)
    {
        if (kind == TransporterKind.Tcp)
        {
            var server = new TcpLoopbackServer
            {
                ClientHandler = (stream, ct) => clientHandler(stream, ct),
            };
            server.Start();
            return new LoopbackSession { TcpServer = server, WebSocketServer = null, Port = server.Port };
        }

        var wsServer = new WebSocketLoopbackServer
        {
            ClientHandler = (socket, ct) => clientHandler(socket, ct),
        };
        wsServer.Start();
        return new LoopbackSession { TcpServer = null, WebSocketServer = wsServer, Port = wsServer.Port };
    }

    public async ValueTask DisposeAsync()
    {
        if (TcpServer != null)
        {
            await TcpServer.DisposeAsync().ConfigureAwait(false);
        }

        if (WebSocketServer != null)
        {
            await WebSocketServer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
