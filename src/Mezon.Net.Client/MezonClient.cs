using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
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

        /// <inheritdoc />
        public override long Latency { get; protected set; }
        /// <inheritdoc />
        public override ConnectionState ConnectionState => _connection.State;

        public int PendingSocketRequestCount =>
            ApiClient is MezonSocketApiClient socket ? socket.PendingSocketRequestCount : 0;

        public MezonClient() : this(new MezonSocketClientOptions())
        {
        }

        public MezonClient(MezonSocketClientOptions options) : this(options, CreateSocketApiClient(options))
        {
        }

        public MezonClient(MezonSocketClientOptions options, IMezonSocketClient socketClient) : base(options, socketClient)
        {
            _stateLock = new SemaphoreSlim(1, 1);
            _socketLogger = LogManager.CreateLogger("MezonSocketClient");
            if (ApiClient is MezonSocketApiClient socketApiClient)
            {
                socketApiClient.ConfigureSocketLogging(LogManager);
            }

            _heartbeatTimes = new ConcurrentQueue<long>();
            HandlerTimeout = options.SocketHandlerTimeoutInMilliseconds;
            _connection = new SocketConnectionManager(
                _stateLock,
                _socketLogger,
                options.ConnectionTimeoutInMilliseconds,
               OnConnectingAsync,
               OnDisconnectingAsync,
               x => ApiClient.DisconnectedEvent += x);
            _connection.Connected += () => TimedInvokeAsync(_connectedEvent, nameof(Connected));
            _connection.Disconnected += (ex, recon) => TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex);
            ApiClient.SocketSentMessageEvent += async msg => await _socketLogger.DebugAsync(msg).ConfigureAwait(false);
            ApiClient.ReceivedMessageEvent += ProcessMessageAsync;
        }

        private static MezonSocketApiClient CreateSocketApiClient(MezonSocketClientOptions options)
            => new MezonSocketApiClient(options.RestClientProvider, options.NetworkTransportProvider, options);

        private async Task OnConnectingAsync()
        {
            try
            {
                await _socketLogger.DebugAsync("Connecting MezonSocket").ConfigureAwait(false);
                await ApiClient.ConnectAsync().ConfigureAwait(false);
                await ApiClient.Heartbeat().ConfigureAwait(false);
                if (ApiClient.LatencyMilliseconds > 0)
                {
                    Latency = ApiClient.LatencyMilliseconds;
                }

                await TimedInvokeAsync(_readyEvent, nameof(ReadyEvent)).ConfigureAwait(false);
                _ = _connection.CompleteAsync();
            }
            catch (Exception ex)
            {
                await _socketLogger.ErrorAsync("Socket connect failed", ex).ConfigureAwait(false);
                throw;
            }
            finally
            {
                await _socketLogger.DebugAsync("Connected MezonSocket").ConfigureAwait(false);
            }
        }

        private void StartHeartbeatLoop()
        {
            if (_heartbeatTask != null)
            {
                return;
            }

            _heartbeatTask = RunHeartbeatAsync(_connection.CancelToken);
        }

        private async Task OnDisconnectingAsync(Exception ex)
        {
            await _socketLogger.DebugAsync("Disconnecting MezonSocket").ConfigureAwait(false);
            await ApiClient.DisconnectAsync(ex).ConfigureAwait(false);

            await _socketLogger.DebugAsync("Waiting for heartbeater").ConfigureAwait(false);
            var heartbeatTask = _heartbeatTask;
            if (heartbeatTask != null)
            {
                await heartbeatTask.ConfigureAwait(false);
            }

            _heartbeatTask = null;

            while (_heartbeatTimes.TryDequeue(out _))
            { }
            await _socketLogger.DebugAsync("Disconnected MezonSocket").ConfigureAwait(false);
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
                await _socketLogger.DebugAsync("Heartbeat loop scheduled").ConfigureAwait(false);
                await Task.Delay(Options.HeartbeatIntervalInMilliseconds, cancelToken).ConfigureAwait(false);
                await _socketLogger.DebugAsync("Heartbeat loop started").ConfigureAwait(false);

                while (!cancelToken.IsCancellationRequested)
                {
                    var heartbeatSucceeded = false;
                    try
                    {
                        var heartbeatOptions = RequestOptions.CreateOrClone(null);
                        heartbeatOptions.SocketSendTimeout = Options.HeartbeatIntervalInMilliseconds;
                        await ApiClient.Heartbeat(heartbeatOptions).ConfigureAwait(false);
                        if (ApiClient.LatencyMilliseconds > 0)
                        {
                            Latency = ApiClient.LatencyMilliseconds;
                        }

                        heartbeatSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        await _socketLogger.WarningAsync("Heartbeat Errored", ex).ConfigureAwait(false);
                    }

                    var effectiveLatency = Math.Min(Latency, Options.HeartbeatIntervalInMilliseconds);
                    var delay = heartbeatSucceeded
                        ? (int)Math.Max(0, Options.HeartbeatIntervalInMilliseconds - effectiveLatency)
                        : Options.HeartbeatIntervalInMilliseconds;
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
            await _connection.WaitAsync().ConfigureAwait(false);
            StartHeartbeatLoop();
        }

        public override async Task DisconnectAsync()
        {
            await _connection.DisconnectAsync().ConfigureAwait(false);
        }

        public async Task<AuthenticationResponse> AuthenticateEmailAsync(string email, string password)
        {
            return await ApiClient.AuthenticateEmailAsync(Options.ServerKey, "", new EmailAuthenticationRequest
            {
                Account = new AccountEmailRequest
                {
                    Email = email,
                    Password = password
                },
            }).ConfigureAwait(false);
        }

        public async Task<Mezon.Net.Api.LoginIDResponse> CreateQRLoginAsync(LoginIDRequest request)
        {
            return await ApiClient.CreateQRLoginAsync(Options.ServerKey, "", request).ConfigureAwait(false);
        }

        public Task<TResponse> SendSocketApiAsync<TRequest, TResponse>(
            string apiName,
            TRequest request,
            Google.Protobuf.MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TRequest : Google.Protobuf.IMessage<TRequest>
            where TResponse : Google.Protobuf.IMessage<TResponse>
        {
            if (ApiClient is MezonSocketApiClient socketClient)
            {
                return socketClient.SendApiAsync(apiName, request, responseParser, options);
            }

            throw new InvalidOperationException("Socket API requires a connected MezonSocketApiClient.");
        }

        public Task<Mezon.Net.Internal.Realtime.Envelope> SendRealtimeAsync(Mezon.Net.Internal.Realtime.Envelope envelope, RequestOptions? options = null)
        {
            if (ApiClient is MezonSocketApiClient socketClient)
            {
                return socketClient.SendEnvelopeAsync(envelope, options);
            }

            throw new InvalidOperationException("Realtime socket requires MezonSocketApiClient.");
        }

        public Task<ClanDescList> GetClanDescriptionAsync(PaginationParams paginationParams)
        {
            return ApiClient.ListClanDescsAsync(paginationParams);
        }
    }
}
