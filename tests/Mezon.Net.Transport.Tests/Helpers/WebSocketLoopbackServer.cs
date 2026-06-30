using System.Net;
using System.Net.WebSockets;

namespace Mezon.Net.Transport.Tests.Helpers;

internal sealed class WebSocketLoopbackServer : IAsyncDisposable
{
    private HttpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;

    public int Port { get; private set; }

    public Func<System.Net.WebSockets.WebSocket, CancellationToken, Task>? ClientHandler { get; set; }

    public void Start()
    {
        Port = TcpLoopbackServer.ReserveLoopbackPort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            if (ClientHandler == null)
            {
                context.Response.StatusCode = 503;
                context.Response.Close();
                continue;
            }

            _ = Task.Run(async () =>
            {
                System.Net.WebSockets.WebSocket? socket = null;
                try
                {
                    var wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                    socket = wsContext.WebSocket;
                    await ClientHandler(socket, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    if (socket != null)
                    {
                        if (socket.State == WebSocketState.Open)
                        {
                            try
                            {
                                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
                            }
                            catch
                            {
                            }
                        }

                        socket.Dispose();
                    }
                }
            }, CancellationToken.None);
        }
    }

    public static string? ReadTokenFromQuery(HttpListenerRequest request)
    {
        var raw = request.Url?.Query;
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        foreach (var segment in raw.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "token")
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    public static async Task<byte[]> ReadBinaryMessageAsync(System.Net.WebSockets.WebSocket socket, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.Count > 0)
            {
                ms.Write(buffer, 0, result.Count);
            }
        }
        while (!result.EndOfMessage);

        return ms.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
        }

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }
}
