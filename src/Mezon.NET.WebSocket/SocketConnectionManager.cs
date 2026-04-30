using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.WebSocket
{
    internal class SocketConnectionManager : IDisposable
    {
        public event Func<Task> Connected { add { _connectedEvent.Add(value); } remove { _connectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _connectedEvent = new AsyncEvent<Func<Task>>();
        public event Func<Exception, bool, Task> Disconnected { add { _disconnectedEvent.Add(value); } remove { _disconnectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, bool, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, bool, Task>>();

        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _logger;
        private readonly int _connectionTimeoutInMilliseconds;
        private readonly Func<Task> _onConnecting;
        private readonly Func<Exception, Task> _onDisconnecting;

        private TaskCompletionSource<bool> _connectionPromise, _readyPromise;
        private CancellationTokenSource _combinedCancelToken, _reconnectCancelToken, _connectionCancelToken;
        private Task _task;

        private bool _isDisposed;

        public ConnectionState State { get; private set; }
        public CancellationToken CancelToken { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        internal SocketConnectionManager(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
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

            clientDisconnectHandler(ex =>
            {
                if (ex != null)
                {
                    var ex2 = ex as WebSocketClosedException;
                    if (ex2?.CloseCode == 4006)
                    {
                        CriticalError(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "WebSocket session expired", ex));
                    }
                    else if (ex2?.CloseCode == 4014)
                    {
                        CriticalError(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "WebSocket connection was closed", ex));
                    }
                    else
                    {
                        Error(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "WebSocket connection was closed", ex));
                    }
                }
                else
                {
                    Error(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "WebSocket connection was closed"));
                }

                return Task.CompletedTask;
            });
        }

        public async Task ConnectAsync()
        {
            if (State != ConnectionState.Disconnected)
            {
                throw new InvalidOperationException("Cannot start an already running client.");
            }

            await AcquireConnectionLock().ConfigureAwait(false);
            var reconnectCancelToken = new CancellationTokenSource();
            _reconnectCancelToken?.Dispose();
            _reconnectCancelToken = reconnectCancelToken;
            _task = Task.Run(async () =>
            {
                try
                {
                    Random jitter = new Random();
                    int nextReconnectDelay = 1000;
                    while (!reconnectCancelToken.IsCancellationRequested)
                    {
                        try
                        {
                            await ConnectInternalAsync(reconnectCancelToken).ConfigureAwait(false);
                            nextReconnectDelay = 1000;
                            await _connectionPromise.Task.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException ex)
                        {
                            Cancel();
                            await DisconnectInternalAsync(ex, !reconnectCancelToken.IsCancellationRequested).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Error(ex);
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
                            //Wait before reconnecting
                            await Task.Delay(nextReconnectDelay, reconnectCancelToken.Token).ConfigureAwait(false);
                            nextReconnectDelay = (nextReconnectDelay * 2) + jitter.Next(-250, 250);
                            if (nextReconnectDelay > 60000)
                            {
                                nextReconnectDelay = 60000;
                            }
                        }
                    }
                }
                finally
                {
                    _stateLock.Release();
                }
            });
        }

        public Task DisconnectAsync()
        {
            Cancel();
            return Task.CompletedTask;
        }

        public Task CompleteAsync() => Task.Run(() => _readyPromise.TrySetResult(true));

        public Task WaitAsync() => _readyPromise.Task;

        public void Cancel()
        {
            _readyPromise?.TrySetCanceled();
            _connectionPromise?.TrySetCanceled();
            _reconnectCancelToken?.Cancel();
            _connectionCancelToken?.Cancel();
        }

        public void Error(Exception ex)
        {
            _readyPromise.TrySetException(ex);
            _connectionPromise.TrySetException(ex);
            _connectionCancelToken?.Cancel();
        }

        public void CriticalError(Exception ex)
        {
            _reconnectCancelToken?.Cancel();
            Error(ex);
        }

        public void Reconnect()
        {
            _readyPromise.TrySetCanceled();
            _connectionPromise.TrySetCanceled();
            _connectionCancelToken?.Cancel();
        }

        private async Task ConnectInternalAsync(CancellationTokenSource reconnectCancelToken)
        {
            _connectionCancelToken?.Dispose();
            _combinedCancelToken?.Dispose();
            _connectionCancelToken = new CancellationTokenSource();
            _combinedCancelToken = CancellationTokenSource.CreateLinkedTokenSource(_connectionCancelToken.Token, reconnectCancelToken.Token);
            CancelToken = _combinedCancelToken.Token;

            _connectionPromise = new TaskCompletionSource<bool>();
            State = ConnectionState.Connecting;
            await _logger.InfoAsync("Connecting").ConfigureAwait(false);

            try
            {
                var readyPromise = new TaskCompletionSource<bool>();
                _readyPromise = readyPromise;

                var cancelToken = CancelToken;
                var _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_connectionTimeoutInMilliseconds, cancelToken).ConfigureAwait(false);
                        readyPromise.TrySetException(new TimeoutException());
                    }
                    catch (OperationCanceledException) { }
                });

                await _onConnecting().ConfigureAwait(false);

                await _logger.InfoAsync("Connected").ConfigureAwait(false);
                State = ConnectionState.Connected;
                await _connectedEvent.InvokeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
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
            await _disconnectedEvent.InvokeAsync(ex, isReconnecting).ConfigureAwait(false);
            await _logger.InfoAsync("Disconnected").ConfigureAwait(false);
        }

        private async Task AcquireConnectionLock()
        {
            while (true)
            {
                await DisconnectAsync().ConfigureAwait(false);
                if (await _stateLock.WaitAsync(0).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _combinedCancelToken?.Dispose();
                    _reconnectCancelToken?.Dispose();
                    _connectionCancelToken?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}

