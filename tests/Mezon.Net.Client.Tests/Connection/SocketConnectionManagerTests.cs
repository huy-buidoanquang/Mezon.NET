using System.Net.WebSockets;
using Mezon.Net.Client.Tests.Helpers;
using Mezon.Net.Core;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.Client.Tests.Connection;

public sealed class SocketConnectionManagerTests
{
    [Fact]
    public async Task ConnectAsync_first_success_sets_connected_and_wait_completes()
    {
        var host = CreateHost(onConnecting: () => Task.CompletedTask);
        await host.Manager.ConnectAsync().ConfigureAwait(false);
        await host.Manager.WaitAsync().ConfigureAwait(false);

        Assert.Equal(ConnectionState.Connected, host.Manager.State);
        Assert.Equal(1, host.ConnectingCount);
    }

    [Fact]
    public async Task WaitAsync_throws_when_onConnecting_fails()
    {
        var host = CreateHost(onConnecting: () => throw new InvalidOperationException("connect failed"));
        host.Manager.ReconnectBaseDelayMs = 50_000;
        host.Manager.MaxReconnectDelayMs = 50_000;
        await host.Manager.ConnectAsync().ConfigureAwait(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Manager.WaitAsync()).ConfigureAwait(false);
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    [Fact]
    public async Task WaitAsync_throws_timeout_when_onConnecting_hangs()
    {
        var host = CreateHost(
            connectionTimeoutMs: 200,
            onConnecting: () => Task.Delay(1_000));
        host.Manager.ReconnectBaseDelayMs = 50_000;
        host.Manager.MaxReconnectDelayMs = 50_000;
        await host.Manager.ConnectAsync().ConfigureAwait(false);

        await Assert.ThrowsAsync<TimeoutException>(() => host.Manager.WaitAsync()).ConfigureAwait(false);
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    [Fact]
    public async Task Transport_error_while_connected_fires_disconnected_then_reconnecting()
    {
        var host = CreateHost(onConnecting: () => Task.CompletedTask);
        await host.Manager.ConnectAsync().ConfigureAwait(false);
        await host.Manager.WaitAsync().ConfigureAwait(false);

        host.RaiseTransportDisconnected(new Exception("drop"));

        await host.DisconnectedArgs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        await host.ReconnectingArgs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        Assert.Equal(1, host.DisconnectingCount);
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    [Fact]
    public async Task CriticalError_stops_reconnect_loop()
    {
        var host = CreateHost(onConnecting: () => Task.CompletedTask);
        host.Manager.ReconnectBaseDelayMs = 50;
        host.Manager.MaxReconnectDelayMs = 50;
        await host.Manager.ConnectAsync().ConfigureAwait(false);
        await host.Manager.WaitAsync().ConfigureAwait(false);

        host.RaiseTransportDisconnected(new SocketClosedException(4006));

        await host.DisconnectedArgs.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        Assert.False(host.ReconnectingArgs.Task.IsCompleted);
        await host.Manager.DisconnectAsync().ConfigureAwait(false);
        Assert.Equal(ConnectionState.Disconnected, host.Manager.State);
    }

    [Fact]
    public async Task DisconnectAsync_is_awaitable_and_stops_loop()
    {
        var host = CreateHost(onConnecting: () => Task.CompletedTask);
        await host.Manager.ConnectAsync().ConfigureAwait(false);
        await host.Manager.WaitAsync().ConfigureAwait(false);

        await host.Manager.DisconnectAsync().ConfigureAwait(false);

        Assert.Equal(ConnectionState.Disconnected, host.Manager.State);
    }

    [Fact]
    public async Task ConnectAsync_while_connecting_throws()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = CreateHost(onConnecting: () => gate.Task);
        await host.Manager.ConnectAsync().ConfigureAwait(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Manager.ConnectAsync()).ConfigureAwait(false);

        gate.TrySetResult();
        await host.Manager.DisconnectAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Reconnect_cancels_active_connection_and_attempts_again()
    {
        var connectCount = 0;
        var host = CreateHost(onConnecting: () =>
        {
            Interlocked.Increment(ref connectCount);
            return Task.CompletedTask;
        });
        host.Manager.ReconnectBaseDelayMs = 30;
        host.Manager.MaxReconnectDelayMs = 30;
        await host.Manager.ConnectAsync().ConfigureAwait(false);
        await host.Manager.WaitAsync().ConfigureAwait(false);
        Assert.Equal(1, connectCount);

        host.Manager.Reconnect();

        var deadline = Environment.TickCount64 + 3000;
        while (connectCount < 2 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.True(connectCount >= 2);
        await host.Manager.DisconnectAsync().ConfigureAwait(false);
    }

    private static TestHost CreateHost(
        Func<Task>? onConnecting = null,
        int connectionTimeoutMs = 5000)
    {
        return new TestHost(onConnecting ?? (() => Task.CompletedTask), connectionTimeoutMs);
    }

    private sealed class TestHost
    {
        private Func<Exception, Task>? _transportDisconnect;

        public SocketConnectionManager Manager { get; }
        public TaskCompletionSource<Exception> DisconnectedArgs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Exception> ReconnectingArgs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConnectingCount { get; private set; }
        public int DisconnectingCount { get; private set; }

        public TestHost(Func<Task> onConnecting, int connectionTimeoutMs)
        {
            var logger = new LogManager(LogLevel.Error).CreateLogger("scm-test");
            Manager = new SocketConnectionManager(
                new SemaphoreSlim(1, 1),
                logger,
                connectionTimeoutMs,
                async () =>
                {
                    ConnectingCount++;
                    await onConnecting().ConfigureAwait(false);
                },
                _ =>
                {
                    DisconnectingCount++;
                    return Task.CompletedTask;
                },
                register => _transportDisconnect = register);

            Manager.Disconnected += ex =>
            {
                DisconnectedArgs.TrySetResult(ex);
                return Task.CompletedTask;
            };
            Manager.Reconnecting += ex =>
            {
                ReconnectingArgs.TrySetResult(ex);
                return Task.CompletedTask;
            };
        }

        public void RaiseTransportDisconnected(Exception ex) => _transportDisconnect?.Invoke(ex);
    }
}
