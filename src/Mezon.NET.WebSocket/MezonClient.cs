using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Logging;

namespace Mezon.NET.WebSocket
{
    public partial class MezonClient : BaseSocketClient, IMezonClient
    {
        private readonly SocketConnectionManager _connection;
        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _socketLogger;
        private readonly ConcurrentQueue<long> _heartbeatTimes;

        private Task? _heartbeatTask;
        private long _lastMessageTime;
        internal int? HandlerTimeout { get; private set; }

        public override Api.MezonClient RestClient { get; }
        /// <inheritdoc />
        public override long Latency { get; protected set; }
        /// <inheritdoc />
        public override ConnectionState ConnectionState => _connection.State;

        public MezonClient() : this(new MezonSocketClientConfiguration())
        {
        }

        public MezonClient(MezonSocketClientConfiguration configuration) : this(configuration, CreateSocketApiClient(configuration))
        {
        }

        public MezonClient(MezonSocketClientConfiguration configuration, IMezonSocketClient socketClient) : base(configuration, socketClient)
        {
            _stateLock = new SemaphoreSlim(1, 1);
            _socketLogger = LogManager.CreateLogger("MezonSocketClient");
            _heartbeatTimes = new ConcurrentQueue<long>();
            RestClient = new Api.MezonClient(configuration, ApiClient);
            HandlerTimeout = configuration.SocketHandlerTimeoutInMilliseconds;
            _connection = new SocketConnectionManager(
                _stateLock,
                _socketLogger,
                configuration.ConnectionTimeoutInMilliseconds,
               OnConnectingAsync,
               OnDisconnectingAsync,
               x => ApiClient.DisconnectedEvent += x);
            _connection.Connected += () => TimedInvokeAsync(_connectedEvent, nameof(Connected));
            _connection.Disconnected += (ex, recon) => TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex);
            ApiClient.SocketSentMessageEvent += async msg => await _socketLogger.DebugAsync(msg).ConfigureAwait(false);
            ApiClient.ReceivedMessageEvent += ProcessMessageAsync;
        }

        private static MezonSocketApiClient CreateSocketApiClient(MezonSocketClientConfiguration configuration)
            => new MezonSocketApiClient(configuration.HttpClientProvider, configuration.GRPCClientProvider, configuration.WebSocketClientProvider, configuration);

        private async Task OnConnectingAsync()
        {
            try
            {
                await _socketLogger.TraceAsync("Connecting MezonSocket").ConfigureAwait(false);
                await ApiClient.ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _socketLogger.TraceAsync($"Error {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                await _socketLogger.TraceAsync("Connected MezonSocket").ConfigureAwait(false);
            }

            // TODO:
            // Wait for PONG event or connection timeout before allowing next connection attempt
            await _connection.WaitAsync().ConfigureAwait(false);
        }

        private async Task OnDisconnectingAsync(Exception ex)
        {
            await _socketLogger.TraceAsync("Disconnecting MezonSocket").ConfigureAwait(false);
            await ApiClient.DisconnectAsync(ex).ConfigureAwait(false);

            //Wait for tasks to complete
            await _socketLogger.TraceAsync("Waiting for heartbeater").ConfigureAwait(false);
            var heartbeatTask = _heartbeatTask;
            if (heartbeatTask != null)
            {
                await heartbeatTask.ConfigureAwait(false);
            }

            _heartbeatTask = null;

            while (_heartbeatTimes.TryDequeue(out _))
            { }
            await _socketLogger.TraceAsync("Disconnected MezonSocket").ConfigureAwait(false);
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

        private async Task TimeoutWrap(string name, Func<Task> action)
        {
            try
            {
                if (!HandlerTimeout.HasValue)
                {
                    return;
                }

                var timeoutTask = Task.Delay(HandlerTimeout.Value);
                var handlersTask = action();
                if (await Task.WhenAny(timeoutTask, handlersTask).ConfigureAwait(false) == timeoutTask)
                {
                    await _socketLogger.WarningAsync($"A {name} handler is blocking the socket task.").ConfigureAwait(false);
                }
                await handlersTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _socketLogger.WarningAsync($"A {name} handler has thrown an unhandled exception.", ex).ConfigureAwait(false);
            }
        }

        private async Task RunHeartbeatAsync(CancellationToken cancelToken)
        {
            try
            {
                await _socketLogger.DebugAsync("Heartbeat Started").ConfigureAwait(false);
                while (!cancelToken.IsCancellationRequested)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (_heartbeatTimes.TryPeek(out long oldestHeartBeat))
                    {
                        if ((now - oldestHeartBeat) > Configuration.HeartbeatIntervalInMilliseconds)
                        {
                            if (ConnectionState == ConnectionState.Connected)
                            {
                                _connection.Error(new Exception("Server missed last heartbeat"));
                                return;
                            }
                        }
                    }

                    _heartbeatTimes.Enqueue(now);
                    try
                    {
                        await ApiClient.Ping().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await _socketLogger.WarningAsync("Heartbeat Errored", ex).ConfigureAwait(false);
                    }

                    int delay = (int)Math.Max(0, Configuration.HeartbeatIntervalInMilliseconds - Latency);
                    await Task.Delay(delay, cancelToken).ConfigureAwait(false);
                }
                await _socketLogger.DebugAsync("Heartbeat Stopped").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await _socketLogger.DebugAsync("Heartbeat Stopped").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _socketLogger.ErrorAsync("Heartbeat Errored", ex).ConfigureAwait(false);
            }
        }

        public override async Task ConnectAsync()
        {
            await _connection.ConnectAsync().ConfigureAwait(false);
        }

        public override async Task DisconnectAsync()
        {
            await _connection.DisconnectAsync().ConfigureAwait(false);
        }
    }
}
