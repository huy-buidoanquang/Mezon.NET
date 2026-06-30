using System.Net;
using System.Net.Sockets;

namespace Mezon.Net.Transport.Tests.Helpers;

internal sealed class TcpLoopbackServer : IAsyncDisposable
{
    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;

    public int Port { get; private set; }

    public Func<NetworkStream, CancellationToken, Task>? ClientHandler { get; set; }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptTask = AcceptLoopAsync(_cts.Token);
    }

    public static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (ClientHandler == null)
            {
                client.Dispose();
                continue;
            }

            _ = Task.Run(async () =>
            {
                await using var stream = client.GetStream();
                try
                {
                    await ClientHandler(stream, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    client.Dispose();
                }
            }, CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
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
