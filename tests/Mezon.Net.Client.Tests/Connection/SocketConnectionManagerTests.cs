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

    [Fact]
    public async Task Backoff_grows_when_connections_die_before_stable_threshold()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentQueue<long>();
        TestHost host = null!;
        host = CreateHost(onConnecting: _ =>
        {
            attempts.Enqueue(Environment.TickCount64);
            DropSoon(host);
            return Task.CompletedTask;
        });
        host.Manager.ReconnectBaseDelayMs = 400;
        host.Manager.MaxReconnectDelayMs = 10_000;
        await host.Manager.ConnectAsync();

        await WaitUntilAsync(() => attempts.Count >= 4, TimeSpan.FromSeconds(8));
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));

        var times = attempts.ToArray();
        // Delays double (400 -> ~800 -> ~1600 ms); before the fix every successful connect reset them to 400 ms.
        Assert.True(times[3] - times[2] >= 800, $"Third reconnect gap was {times[3] - times[2]} ms.");
    }

    [Fact]
    public async Task Backoff_resets_after_stable_connection()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentQueue<long>();
        TestHost host = null!;
        host = CreateHost(onConnecting: _ =>
        {
            attempts.Enqueue(Environment.TickCount64);
            DropSoon(host, delayMs: 150);
            return Task.CompletedTask;
        });
        host.Manager.ReconnectBaseDelayMs = 300;
        host.Manager.MaxReconnectDelayMs = 10_000;
        host.Manager.StableConnectionThresholdMs = 50;
        await host.Manager.ConnectAsync();

        await WaitUntilAsync(() => attempts.Count >= 4, TimeSpan.FromSeconds(8));
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));

        var times = attempts.ToArray();
        Assert.True(times[3] - times[2] < 1_000, $"Third reconnect gap was {times[3] - times[2]} ms.");
    }

    [Fact]
    public async Task Teardown_failure_does_not_stop_reconnect_loop()
    {
        var connectCount = 0;
        var host = new TestHost(
            _ =>
            {
                Interlocked.Increment(ref connectCount);
                return Task.CompletedTask;
            },
            connectionTimeoutMs: 5000,
            onDisconnecting: _ => throw new InvalidOperationException("teardown failed"));
        host.Manager.ReconnectBaseDelayMs = 30;
        host.Manager.MaxReconnectDelayMs = 30;
        await host.Manager.ConnectAsync();
        await host.Manager.WaitAsync();

        host.RaiseTransportDisconnected(new Exception("drop"));

        await WaitUntilAsync(() => Volatile.Read(ref connectCount) >= 2, TimeSpan.FromSeconds(3));
        await WaitUntilAsync(() => host.Manager.State == ConnectionState.Connected, TimeSpan.FromSeconds(3));
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Repeated_unauthorized_closes_stop_reconnecting()
    {
        var connectCount = 0;
        TestHost host = null!;
        host = CreateHost(onConnecting: _ =>
        {
            Interlocked.Increment(ref connectCount);
            DropSoon(host, error: new NetworkTransportUnauthorizationException());
            return Task.CompletedTask;
        });
        host.Manager.ReconnectBaseDelayMs = 20;
        host.Manager.MaxReconnectDelayMs = 20;
        await host.Manager.ConnectAsync();

        await host.Manager.LifecycleTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(SocketConnectionManager.MaxConsecutiveAuthFailures, Volatile.Read(ref connectCount));
        Assert.Equal(ConnectionState.Disconnected, host.Manager.State);
    }

    [Fact]
    public async Task Connect_timeout_cancels_the_connect_token()
    {
        var tokenCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = CreateHost(
            connectionTimeoutMs: 200,
            onConnecting: async ct =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    tokenCancelled.TrySetResult();
                    throw;
                }
            });
        host.Manager.ReconnectBaseDelayMs = 50_000;
        host.Manager.MaxReconnectDelayMs = 50_000;
        await host.Manager.ConnectAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => host.Manager.WaitAsync());
        await tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DisconnectAsync_aborts_a_hung_connect()
    {
        var host = CreateHost(
            connectionTimeoutMs: 60_000,
            onConnecting: ct => Task.Delay(Timeout.Infinite, ct));
        await host.Manager.ConnectAsync();
        await Task.Delay(100);

        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(ConnectionState.Disconnected, host.Manager.State);
    }

    [Fact]
    public async Task Concurrent_error_reconnect_and_cancel_do_not_throw()
    {
        var host = CreateHost(onConnecting: _ => Task.CompletedTask);
        host.Manager.ReconnectBaseDelayMs = 1;
        host.Manager.MaxReconnectDelayMs = 1;
        await host.Manager.ConnectAsync();

        var deadline = Environment.TickCount64 + 500;
        await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(() =>
        {
            while (Environment.TickCount64 < deadline)
            {
                if (i % 2 == 0)
                {
                    host.Manager.Reconnect();
                }
                else
                {
                    host.Manager.Error(new Exception("drop"));
                }
            }
        })));

        host.Manager.Cancel();
        await host.Manager.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ConnectionState.Disconnected, host.Manager.State);
    }

    private static void DropSoon(TestHost host, int delayMs = 20, Exception? error = null)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            host.RaiseTransportDisconnected(error ?? new Exception("drop"));
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }

    private static TestHost CreateHost(
        Func<Task>? onConnecting = null,
        int connectionTimeoutMs = 5000)
    {
        var connect = onConnecting ?? (() => Task.CompletedTask);
        return new TestHost(_ => connect(), connectionTimeoutMs);
    }

    private static TestHost CreateHost(
        Func<CancellationToken, Task> onConnecting,
        int connectionTimeoutMs = 5000)
    {
        return new TestHost(onConnecting, connectionTimeoutMs);
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

        public TestHost(
            Func<CancellationToken, Task> onConnecting,
            int connectionTimeoutMs,
            Func<Exception, Task>? onDisconnecting = null)
        {
            var logger = new LogManager(LogLevel.Error).CreateLogger("scm-test");
            Manager = new SocketConnectionManager(
                new SemaphoreSlim(1, 1),
                logger,
                connectionTimeoutMs,
                async ct =>
                {
                    ConnectingCount++;
                    await onConnecting(ct).ConfigureAwait(false);
                },
                ex =>
                {
                    DisconnectingCount++;
                    return onDisconnecting?.Invoke(ex) ?? Task.CompletedTask;
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
