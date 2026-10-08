using System;
using System.Diagnostics;
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

        /// <summary>Consecutive unauthorized closes after which reconnecting stops with a critical error.</summary>
        internal const int MaxConsecutiveAuthFailures = 3;

        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _logger;
        private readonly int _connectionTimeoutInMilliseconds;
        private readonly Func<CancellationToken, Task> _onConnecting;
        private readonly Func<Exception, Task> _onDisconnecting;
        private readonly Func<Exception?, CancellationToken, Task>? _beforeReconnect;

        private TaskCompletionSource<bool> _connectionPromise = default!;
        private TaskCompletionSource<bool> _readyPromise = default!;
        private CancellationTokenSource? _combinedCancelToken;
        private CancellationTokenSource? _reconnectCancelToken;
        private CancellationTokenSource? _connectionCancelToken;
        private Task? _task;
        private TaskCompletionSource<object?>? _lifecycleTcs;

        private bool _isDisposed;

        /// <summary>Initial reconnect backoff for tests; production default is 1000ms.</summary>
        internal int ReconnectBaseDelayMs { get; set; } = 1000;

        /// <summary>Maximum reconnect backoff for tests; production default is 30000ms.</summary>
        internal int MaxReconnectDelayMs { get; set; } = 30000;

        /// <summary>
        /// A connection must stay up this long before the backoff resets. Reaching "connected" only means the TCP
        /// handshake was written, so a server that accepts and immediately closes must not cause a reconnect per second.
        /// </summary>
        internal int StableConnectionThresholdMs { get; set; } = 30000;

        public ConnectionState State { get; private set; }
        public CancellationToken CancelToken { get; private set; }

        internal Task LifecycleTask => _lifecycleTcs?.Task ?? Task.CompletedTask;

#pragma warning disable CS8618
        internal SocketConnectionManager(
#pragma warning restore CS8618
            SemaphoreSlim stateLock,
            Logger logger,
            int connectionTimeoutInMilliseconds,
            Func<CancellationToken, Task> onConnecting,
            Func<Exception, Task> onDisconnecting,
            Action<Func<Exception, Task>> clientDisconnectHandler,
            Func<Exception?, CancellationToken, Task>? beforeReconnect = null)
        {
            _stateLock = stateLock;
            _logger = logger;
            _connectionTimeoutInMilliseconds = connectionTimeoutInMilliseconds;
            _onConnecting = onConnecting;
            _onDisconnecting = onDisconnecting;
            _beforeReconnect = beforeReconnect;
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
            var previousReconnectCancelToken = Interlocked.Exchange(ref _reconnectCancelToken, reconnectCancelToken);
            previousReconnectCancelToken?.Dispose();
            _readyPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _connectionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifecycleTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _task = Task.Run(() => RunConnectionLoopAsync(reconnectCancelToken));
        }

        private async Task RunConnectionLoopAsync(CancellationTokenSource reconnectCancelToken)
        {
            try
            {
                var jitter = new Random();
                var nextReconnectDelay = ReconnectBaseDelayMs;
                var attempt = 0;
                var consecutiveAuthFailures = 0;
                Exception? lastError = null;
                while (!reconnectCancelToken.IsCancellationRequested)
                {
                    long connectedAt = 0;
                    try
                    {
                        if (attempt++ > 0 && _beforeReconnect != null)
                        {
                            await _beforeReconnect(lastError, reconnectCancelToken.Token).ConfigureAwait(false);
                        }

                        await ConnectInternalAsync(reconnectCancelToken).ConfigureAwait(false);
                        connectedAt = Stopwatch.GetTimestamp();
                        await _connectionPromise.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        lastError = ex;
                        await DisconnectInternalAsync(ex, !reconnectCancelToken.IsCancellationRequested).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        var isReconnecting = !reconnectCancelToken.IsCancellationRequested;
                        await LogSafeAsync(isReconnecting ? LogLevel.Warning : LogLevel.Error, "Socket connection failed.", ex).ConfigureAwait(false);
                        await DisconnectInternalAsync(ex, isReconnecting).ConfigureAwait(false);
                    }

                    if (connectedAt != 0 && ElapsedMilliseconds(connectedAt) >= StableConnectionThresholdMs)
                    {
                        nextReconnectDelay = ReconnectBaseDelayMs;
                    }

                    consecutiveAuthFailures = IsUnauthorized(lastError) ? consecutiveAuthFailures + 1 : 0;
                    if (consecutiveAuthFailures >= MaxConsecutiveAuthFailures)
                    {
                        await LogSafeAsync(
                            LogLevel.Error,
                            $"Server rejected the session {consecutiveAuthFailures} times in a row; reconnecting stopped.",
                            lastError).ConfigureAwait(false);
                        CriticalError(lastError!);
                        break;
                    }

                    if (reconnectCancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        await Task.Delay(nextReconnectDelay, reconnectCancelToken.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    nextReconnectDelay = Math.Max(1, Math.Min(MaxReconnectDelayMs, (nextReconnectDelay * 2) + jitter.Next(-250, 250)));
                }
            }
            finally
            {
                _stateLock.Release();
                _lifecycleTcs?.TrySetResult(null);
            }
        }

        private static long ElapsedMilliseconds(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000 / Stopwatch.Frequency;

        internal static bool IsUnauthorized(Exception? ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is NetworkTransportUnauthorizationException
                    || current is MezonAuthenticationException
                    || current is HttpException { HttpCode: System.Net.HttpStatusCode.Unauthorized })
                {
                    return true;
                }
            }

            return false;
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
            TryCancel(Volatile.Read(ref _reconnectCancelToken));
            TryCancel(Volatile.Read(ref _connectionCancelToken));
        }

        public void Error(Exception ex)
        {
            _readyPromise?.TrySetException(ex);
            _connectionPromise?.TrySetException(ex);
            TryCancel(Volatile.Read(ref _connectionCancelToken));
        }

        public void CriticalError(Exception ex)
        {
            TryCancel(Volatile.Read(ref _reconnectCancelToken));
            Error(ex);
        }

        /// <summary>
        /// Soft-reconnect: drops the active connection without stopping the reconnect loop.
        /// </summary>
        public void Reconnect()
        {
            TryCancel(Volatile.Read(ref _connectionCancelToken));
            _connectionPromise?.TrySetCanceled();
        }

        /// <summary>Cancel may race a connect attempt that swaps and disposes the token source.</summary>
        private static void TryCancel(CancellationTokenSource? cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ConnectInternalAsync(CancellationTokenSource reconnectCancelToken)
        {
            // Publish the new token sources before disposing the old ones so a concurrent Cancel/Error/Reconnect sees
            // either a live source or catches ObjectDisposedException.
            var connectionCancelToken = new CancellationTokenSource();
            var combinedCancelToken = CancellationTokenSource.CreateLinkedTokenSource(connectionCancelToken.Token, reconnectCancelToken.Token);
            Interlocked.Exchange(ref _connectionCancelToken, connectionCancelToken)?.Dispose();
            Interlocked.Exchange(ref _combinedCancelToken, combinedCancelToken)?.Dispose();
            CancelToken = combinedCancelToken.Token;

            _connectionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            State = ConnectionState.Connecting;
            await _logger.InfoAsync("Connecting").ConfigureAwait(false);

            if (_readyPromise == null || _readyPromise.Task.IsCompleted)
            {
                _readyPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var readyPromise = _readyPromise;
            using var timeoutCts = new CancellationTokenSource();
            using var timeoutRegistration = timeoutCts.Token.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetException(new TimeoutException()),
                readyPromise);
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, CancelToken);
            timeoutCts.CancelAfter(_connectionTimeoutInMilliseconds);
            try
            {
                try
                {
                    // The token aborts the transport connect on timeout, Cancel, Error or Reconnect.
                    await _onConnecting(connectCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !CancelToken.IsCancellationRequested)
                {
                    throw new TimeoutException();
                }

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
        }

        /// <summary>Tears the connection down. Never throws, so the reconnect loop cannot die here.</summary>
        private async Task DisconnectInternalAsync(Exception ex, bool isReconnecting)
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            State = ConnectionState.Disconnecting;
            try
            {
                await LogSafeAsync(LogLevel.Information, "Disconnecting").ConfigureAwait(false);
                try
                {
                    await _onDisconnecting(ex).ConfigureAwait(false);
                }
                catch (Exception teardownError)
                {
                    await LogSafeAsync(LogLevel.Warning, "Socket teardown failed.", teardownError).ConfigureAwait(false);
                }
            }
            finally
            {
                State = ConnectionState.Disconnected;
            }

            await LogSafeAsync(LogLevel.Information, "Disconnected").ConfigureAwait(false);
            await InvokeSafeAsync(_disconnectedEvent, ex).ConfigureAwait(false);
            if (isReconnecting)
            {
                await InvokeSafeAsync(_reconnectingEvent, ex).ConfigureAwait(false);
                await LogSafeAsync(LogLevel.Information, "Reconnecting").ConfigureAwait(false);
            }
        }

        private async Task InvokeSafeAsync(AsyncEvent<Func<Exception, Task>> eventHandler, Exception ex)
        {
            try
            {
                await eventHandler.InvokeAsync(ex).ConfigureAwait(false);
            }
            catch (Exception handlerError)
            {
                await LogSafeAsync(LogLevel.Warning, "A connection event handler failed.", handlerError).ConfigureAwait(false);
            }
        }

        private async Task LogSafeAsync(LogLevel level, string message, Exception? ex = null)
        {
            try
            {
                await _logger.LogAsync(level, message, ex).ConfigureAwait(false);
            }
            catch
            {
                // A failing log sink must not stop the reconnect loop.
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

                    // Disposed only after the loop has stopped (or the wait gave up); concurrent Cancel calls tolerate it.
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
            GC.SuppressFinalize(this);
        }
    }
}
