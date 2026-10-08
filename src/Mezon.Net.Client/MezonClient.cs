using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Client.Dispatch;
using Mezon.Net.Core;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
{
    public partial class MezonClient : BaseMezonSocketClient, IMezonClient
    {
        private readonly SocketConnectionManager _connection;
        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _logger;
        private readonly ConcurrentQueue<long> _heartbeatTimes;
        private readonly RealtimeEventDispatcher? _dispatcher;
        private Task? _heartbeatTask;
        private long _lastMessageTime;
        internal int? HandlerTimeout { get; private set; }
        public ISession CurrentSession => SessionManager.CurrentSession();

        /// <summary>Returns a non-expired session JWT, refreshing when needed.</summary>
        public Task<string> GetOrRefreshAuthTokenAsync() => SessionManager.GetOrRefreshAsync();

        /// <inheritdoc />
        public override long Latency { get; protected set; }
        /// <inheritdoc />
        public override ConnectionState ConnectionState => _connection.State;

        public int PendingSocketRequestCount => ApiClient is MezonSocketClient socket ? socket.PendingSocketRequestCount : 0;

        /// <summary>Realtime events dropped because their ordered dispatch lane was full.</summary>
        public long DroppedRealtimeEventCount => _dispatcher?.DroppedCount ?? 0;

        public MezonClient() : this(new MezonSocketClientOptions())
        {
        }

        public MezonClient(MezonSocketClientOptions options) : this(options, CreateSocketClient(options))
        {
        }

        internal MezonClient(MezonSocketClientOptions options, MezonSocketClient socketClient) : base(options, socketClient)
        {
            _stateLock = new SemaphoreSlim(1, 1);
            _logger = LogManager.CreateLogger("MezonSocketClient");
            if (ApiClient is MezonSocketClient socketApiClient)
            {
                socketApiClient.ConfigureSocketLogging(LogManager);
            }

            _heartbeatTimes = new ConcurrentQueue<long>();
            HandlerTimeout = options.SocketHandlerTimeoutInMilliseconds;
            if (options.EventDispatchMode == EventDispatchMode.Ordered)
            {
                _dispatcher = new RealtimeEventDispatcher(
                    options.EventDispatchLaneCount,
                    options.EventDispatchLaneCapacity,
                    options.SocketHandlerTimeoutInMilliseconds,
                    _logger);
            }

            _connection = new SocketConnectionManager(
                _stateLock,
                _logger,
                options.ConnectionTimeoutInMilliseconds,
               OnConnectingAsync,
               OnDisconnectingAsync,
               x => ApiClient.SocketDisconnected += x,
               BeforeReconnectAsync);
            _connection.Connected += SocketConnectedHandlerAsync;
            _connection.Disconnected += SocketDisconnectedHandlerAsync;
            _connection.Reconnecting += SocketReconnectingHandlerAsync;
            ApiClient.MessageReceived += SocketMessageHandlerAsync;
            // TODO: Add socket send logging
            ApiClient.SocketMessageSent += async msg => await _logger.DebugAsync(msg).ConfigureAwait(false);
        }

        private static MezonSocketClient CreateSocketClient(MezonSocketClientOptions options)
            => new MezonSocketClient(options.RestClientProvider, options.NetworkTransportProvider, options);

        private async Task OnConnectingAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _logger.DebugAsync("Connecting MezonSocket").ConfigureAwait(false);
                if (ApiClient is MezonSocketClient socketApiClient)
                {
                    await socketApiClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await ApiClient.ConnectAsync().ConfigureAwait(false);
                }

                await TimedInvokeAsync(_clientReadyEvent, nameof(ClientReadyEvent)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Socket connect failed", ex).ConfigureAwait(false);
                throw;
            }
            finally
            {
                if (ApiClient is MezonSocketClient socketApiClient)
                {
                    socketApiClient.RequestQueue.EndConnectPhase();
                }

                await _logger.DebugAsync("Connected MezonSocket").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Renews the session before a reconnect attempt: always after the server rejected the token, otherwise only
        /// when it is about to expire. Never throws, so a failed refresh still counts the connect attempt.
        /// </summary>
        private async Task BeforeReconnectAsync(Exception? lastError, CancellationToken cancellationToken)
        {
            try
            {
                await Sessions.EnsureFreshAsync(force: SocketConnectionManager.IsUnauthorized(lastError)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync("Session refresh before reconnect failed.", ex).ConfigureAwait(false);
            }
        }

        private async Task OnDisconnectingAsync(Exception ex)
        {
            await _logger.DebugAsync("Disconnecting MezonSocket").ConfigureAwait(false);
            await ApiClient.DisconnectAsync(ex).ConfigureAwait(false);

            await _logger.DebugAsync("Waiting for heartbeater").ConfigureAwait(false);
            var heartbeatTask = _heartbeatTask;
            if (heartbeatTask != null)
            {
                await heartbeatTask.ConfigureAwait(false);
            }

            _heartbeatTask = null;

            while (_heartbeatTimes.TryDequeue(out _))
            { }
            await _logger.DebugAsync("Disconnected MezonSocket").ConfigureAwait(false);
        }

        private async Task SocketConnectedHandlerAsync()
        {
            if (_heartbeatTask is { IsCompleted: false })
            {
                return;
            }

            _heartbeatTask = RunHeartbeatAsync(_connection.CancelToken);
            _ = ObserveLifecycleEventAsync(TimedInvokeAsync(_connectedEvent, nameof(Connected)), nameof(Connected));
        }

        private async Task SocketDisconnectedHandlerAsync(Exception ex)
        {
            _ = ObserveLifecycleEventAsync(TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex), nameof(Disconnected));
        }

        private async Task SocketReconnectingHandlerAsync(Exception ex)
        {
            _ = ObserveLifecycleEventAsync(TimedInvokeAsync(_reconnectingEvent, nameof(Reconnecting), ex), nameof(Reconnecting));
        }

        /// <summary>Lifecycle events are not awaited by the connection loop; log their failures instead of losing them.</summary>
        private async Task ObserveLifecycleEventAsync(Task invocation, string name)
        {
            try
            {
                await invocation.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync($"A {name} handler has thrown an unhandled exception.", ex).ConfigureAwait(false);
            }
        }

        private async Task RunHeartbeatAsync(CancellationToken cancelToken)
        {
            var intervalMs = Options.HeartbeatIntervalInMilliseconds;
            try
            {
                await _logger.DebugAsync("Heartbeat loop started").ConfigureAwait(false);

                while (!cancelToken.IsCancellationRequested)
                {
                    try
                    {
                        await SendHeartbeatAsync().ConfigureAwait(false);
                        if (ApiClient.LatencyMilliseconds > 0)
                        {
                            Latency = ApiClient.LatencyMilliseconds;
                        }

                        await Task.Delay(intervalMs, cancelToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        await _logger.WarningAsync("Heartbeat Errored", ex).ConfigureAwait(false);
                        if (!cancelToken.IsCancellationRequested && ConnectionState == ConnectionState.Connected)
                        {
                            await _logger.WarningAsync("Heartbeat failed; scheduling reconnect.").ConfigureAwait(false);
                            _heartbeatTask = null;
                            _connection.Reconnect();
                        }

                        return;
                    }
                }

                await _logger.DebugAsync("Heartbeat Stopped").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await _logger.DebugAsync("Heartbeat Stopped").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Heartbeat Errored", ex).ConfigureAwait(false);
            }
        }

        private Task SendHeartbeatAsync()
        {
            var options = RequestOptions.CreateOrClone(null);
            options.SocketSendTimeout = Options.HeartbeatIntervalInMilliseconds;
            return ApiClient.Heartbeat(options);
        }

        public override async Task ConnectAsync()
        {
            await _connection.ConnectAsync().ConfigureAwait(false);
            await _connection.WaitAsync().ConfigureAwait(false);
        }

        public override async Task DisconnectAsync()
        {
            await _connection.DisconnectAsync().ConfigureAwait(false);
        }

        #region INVOKE EVENT WITH HANDLER TIMEOUT
        private async Task TimeoutWrap(string name, Func<Task> action)
        {
            try
            {
                if (!HandlerTimeout.HasValue)
                {
                    await action().ConfigureAwait(false);
                    return;
                }

                var handlersTask = action();
                if (handlersTask.IsCompleted)
                {
                    // Fast path: no timer for handlers that finish synchronously.
                    await handlersTask.ConfigureAwait(false);
                    return;
                }

                using var timeoutCts = new CancellationTokenSource();
                var timeoutTask = Task.Delay(HandlerTimeout.Value, timeoutCts.Token);
                if (await Task.WhenAny(timeoutTask, handlersTask).ConfigureAwait(false) == timeoutTask)
                {
                    await _logger.WarningAsync($"A {name} handler is taking longer than {HandlerTimeout.Value}ms.").ConfigureAwait(false);
                }
                else
                {
                    timeoutCts.Cancel();
                }

                await handlersTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync($"A {name} handler has thrown an unhandled exception.", ex).ConfigureAwait(false);
            }
        }

        private Task TimedInvokeAsync(AsyncEvent<Func<Task>> eventHandler, string name)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, eventHandler.InvokeAsync) : eventHandler.InvokeAsync();
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T>(AsyncEvent<Func<T, Task>> eventHandler, string name, T arg)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg)) : eventHandler.InvokeAsync(arg);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2>(AsyncEvent<Func<T1, T2, Task>> eventHandler, string name, T1 arg1, T2 arg2)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2)) : eventHandler.InvokeAsync(arg1, arg2);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3>(AsyncEvent<Func<T1, T2, T3, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3, T4>(AsyncEvent<Func<T1, T2, T3, T4, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3, arg4);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3, T4, T5>(AsyncEvent<Func<T1, T2, T3, T4, T5, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5);
            }

            return Task.CompletedTask;
        }
        #endregion

        internal void SetReconnectDelayForTests(int delayMs)
        {
            _connection.ReconnectBaseDelayMs = delayMs;
            _connection.MaxReconnectDelayMs = delayMs;
        }

        internal override async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Always stop the loop: while it waits out a reconnect backoff the state is already Disconnected.
                    await DisconnectAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort disconnect before dispose.
                }

                _connection.Dispose();
                if (_dispatcher != null)
                {
                    await _dispatcher.DisposeAsync().ConfigureAwait(false);
                }

                _heartbeatTask = null;
                _stateLock.Dispose();
            }

            await base.DisposeAsync(disposing).ConfigureAwait(false);
        }

        internal override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connection.Dispose();
                _dispatcher?.Cancel();
                _heartbeatTask = null;
            }

            base.Dispose(disposing);
        }
    }
}
