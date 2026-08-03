using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Runs the socket connect / disconnect / auto-reconnect loop for <see cref="MezonClient"/>.
    /// </summary>
    internal class SocketConnectionManager : IDisposable
    {
        public event Func<Task> Connected { add { _connectedEvent.Add(value); } remove { _connectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _connectedEvent = new AsyncEvent<Func<Task>>();
        public event Func<Exception, Task> Disconnected { add { _disconnectedEvent.Add(value); } remove { _disconnectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();
        public event Func<Exception, Task> Reconnecting { add { _reconnectingEvent.Add(value); } remove { _reconnectingEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _reconnectingEvent = new AsyncEvent<Func<Exception, Task>>();

        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _logger;
        private readonly int _connectionTimeoutInMilliseconds;
        private readonly Func<Task> _onConnecting;
        private readonly Func<Exception, Task> _onDisconnecting;

        private TaskCompletionSource<bool> _connectionPromise = default!;
        private TaskCompletionSource<bool> _readyPromise = default!;
        private CancellationTokenSource? _combinedCancelToken;
        private CancellationTokenSource? _reconnectCancelToken;
        private CancellationTokenSource? _connectionCancelToken;
        private CancellationTokenSource? _connectTimeoutCts;
        private Task? _task;
        private TaskCompletionSource<object?>? _lifecycleTcs;

        private bool _isDisposed;

        /// <summary>Initial reconnect backoff for tests; production default is 1000ms.</summary>
        internal int ReconnectBaseDelayMs { get; set; } = 1000;

        /// <summary>Maximum reconnect backoff for tests; production default is 30000ms.</summary>
        internal int MaxReconnectDelayMs { get; set; } = 30000;

        public ConnectionState State { get; private set; }
        public CancellationToken CancelToken { get; private set; }

        internal Task LifecycleTask => _lifecycleTcs?.Task ?? Task.CompletedTask;

#pragma warning disable CS8618
        internal SocketConnectionManager(
#pragma warning restore CS8618
            SemaphoreSlim stateLock,
            Logger logger,
            int connectionTimeoutInMilliseconds,
            Func<Task> onConnecting,
            Func<Exception, Task> onDisconnecting,
            Action<Func<Exception, Task>> clientDisconnectHandler)
        {
            _stateLock = stateLock;
            _logger = logger;
            _connectionTimeoutInMilliseconds = connectionTimeoutInMilliseconds;
            _onConnecting = onConnecting;
            _onDisconnecting = onDisconnecting;
            clientDisconnectHandler(HandleTransportDisconnectedAsync);
        }

        private Task HandleTransportDisconnectedAsync(Exception? ex)
        {
            if (ex != null)
            {
                var closed = ex as SocketClosedException;
                if (closed?.CloseCode == 4006)
                {
                    CriticalError(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Socket session expired", ex));
                }
                else if (closed?.CloseCode == 4014)
                {
                    CriticalError(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Socket connection was closed", ex));
                }
                else
                {
                    Error(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Socket connection was closed", ex));
                }
            }
            else
            {
                Error(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Socket connection was closed"));
            }

            return Task.CompletedTask;
        }

        public async Task ConnectAsync()
        {
            // Guard before Task.Run publishes Connecting — otherwise a second ConnectAsync can race.
            if (State != ConnectionState.Disconnected || (_task != null && !_task.IsCompleted))
            {
                throw new InvalidOperationException("Cannot start an already running client.");
            }

            await AcquireConnectionLock().ConfigureAwait(false);
            var reconnectCancelToken = new CancellationTokenSource();
            _reconnectCancelToken?.Dispose();
            _reconnectCancelToken = reconnectCancelToken;
            _readyPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _connectionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifecycleTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _task = Task.Run(async () =>
            {
                try
                {
                    var jitter = new Random();
                    var nextReconnectDelay = ReconnectBaseDelayMs;
                    while (!reconnectCancelToken.IsCancellationRequested)
                    {
                        try
                        {
                            await ConnectInternalAsync(reconnectCancelToken).ConfigureAwait(false);
                            nextReconnectDelay = ReconnectBaseDelayMs;
                            await _connectionPromise.Task.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException ex)
                        {
                            await DisconnectInternalAsync(ex, !reconnectCancelToken.IsCancellationRequested).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (!reconnectCancelToken.IsCancellationRequested)
                            {
                                await _logger.WarningAsync(ex).ConfigureAwait(false);
                                await DisconnectInternalAsync(ex, true).ConfigureAwait(false);
                            }
                            else
                            {
                                await _logger.ErrorAsync(ex).ConfigureAwait(false);
                                await DisconnectInternalAsync(ex, false).ConfigureAwait(false);
                            }
                        }

                        if (!reconnectCancelToken.IsCancellationRequested)
                        {
                            await Task.Delay(nextReconnectDelay, reconnectCancelToken.Token).ConfigureAwait(false);
                            nextReconnectDelay = (nextReconnectDelay * 2) + jitter.Next(-250, 250);
                            if (nextReconnectDelay > MaxReconnectDelayMs)
                            {
                                nextReconnectDelay = MaxReconnectDelayMs;
                            }
                        }
                    }
                }
                finally
                {
                    _stateLock.Release();
                    _lifecycleTcs?.TrySetResult(null);
                }
            });
        }

        public Task DisconnectAsync()
        {
            Cancel();
            return _lifecycleTcs?.Task ?? Task.CompletedTask;
        }

        public Task WaitAsync() => _readyPromise?.Task ?? Task.CompletedTask;

        public void Cancel()
        {
            _readyPromise?.TrySetCanceled();
            _connectionPromise?.TrySetCanceled();
            _reconnectCancelToken?.Cancel();
            _connectionCancelToken?.Cancel();
            _connectTimeoutCts?.Cancel();
        }

        public void Error(Exception ex)
        {
            _readyPromise?.TrySetException(ex);
            _connectionPromise?.TrySetException(ex);
            _connectionCancelToken?.Cancel();
            _connectTimeoutCts?.Cancel();
        }

        public void CriticalError(Exception ex)
        {
            _reconnectCancelToken?.Cancel();
            Error(ex);
        }

        /// <summary>
        /// Soft-reconnect: drops the active connection without stopping the reconnect loop.
        /// </summary>
        public void Reconnect()
        {
            _connectionCancelToken?.Cancel();
            _connectionPromise?.TrySetCanceled();
        }

        private async Task ConnectInternalAsync(CancellationTokenSource reconnectCancelToken)
        {
            _connectionCancelToken?.Dispose();
            _combinedCancelToken?.Dispose();
            _connectTimeoutCts?.Dispose();
            _connectionCancelToken = new CancellationTokenSource();
            _combinedCancelToken = CancellationTokenSource.CreateLinkedTokenSource(_connectionCancelToken.Token, reconnectCancelToken.Token);
            CancelToken = _combinedCancelToken.Token;

            _connectionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            State = ConnectionState.Connecting;
            await _logger.InfoAsync("Connecting").ConfigureAwait(false);

            if (_readyPromise == null || _readyPromise.Task.IsCompleted)
            {
                _readyPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var readyPromise = _readyPromise;
            _connectTimeoutCts = new CancellationTokenSource();
            var connectTimeoutLinked = CancellationTokenSource.CreateLinkedTokenSource(_connectTimeoutCts.Token, CancelToken);
            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_connectionTimeoutInMilliseconds, connectTimeoutLinked.Token).ConfigureAwait(false);
                        readyPromise.TrySetException(new TimeoutException());
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });

                await _onConnecting().ConfigureAwait(false);
                State = ConnectionState.Connected;
                await _logger.InfoAsync("Connected").ConfigureAwait(false);
                await _connectedEvent.InvokeAsync().ConfigureAwait(false);
                readyPromise.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
            finally
            {
                _connectTimeoutCts.Cancel();
                _connectTimeoutCts.Dispose();
                _connectTimeoutCts = null;
                connectTimeoutLinked.Dispose();
            }
        }

        private async Task DisconnectInternalAsync(Exception ex, bool isReconnecting)
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            State = ConnectionState.Disconnecting;
            await _logger.InfoAsync("Disconnecting").ConfigureAwait(false);

            await _onDisconnecting(ex).ConfigureAwait(false);

            State = ConnectionState.Disconnected;
            await _logger.InfoAsync("Disconnected").ConfigureAwait(false);
            await _disconnectedEvent.InvokeAsync(ex).ConfigureAwait(false);
            if (isReconnecting)
            {
                await _reconnectingEvent.InvokeAsync(ex).ConfigureAwait(false);
                await _logger.InfoAsync("Reconnecting").ConfigureAwait(false);
            }
        }

        private async Task AcquireConnectionLock()
        {
            var priorLifecycle = _lifecycleTcs;
            await DisconnectAsync().ConfigureAwait(false);
            if (priorLifecycle != null)
            {
                await priorLifecycle.Task.ConfigureAwait(false);
            }

            await _stateLock.WaitAsync().ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Cancel();
                    try
                    {
                        _task?.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                    }

                    _combinedCancelToken?.Dispose();
                    _reconnectCancelToken?.Dispose();
                    _connectionCancelToken?.Dispose();
                    _connectTimeoutCts?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
