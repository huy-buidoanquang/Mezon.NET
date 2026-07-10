using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
{
    public partial class MezonClient : BaseSocketClient, IMezonClient
    {
        private readonly SocketConnectionManager _connection;
        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _logger;
        private readonly ConcurrentQueue<long> _heartbeatTimes;
        private Task? _heartbeatTask;
        private long _lastMessageTime;
        internal int? HandlerTimeout { get; private set; }
        public ISession CurrentSession => SessionManager.CurrentSession();

        /// <inheritdoc />
        public override long Latency { get; protected set; }
        /// <inheritdoc />
        public override ConnectionState ConnectionState => _connection.State;

        public int PendingSocketRequestCount => ApiClient is MezonSocketClient socket ? socket.PendingSocketRequestCount : 0;

        public MezonClient() : this(new MezonSocketClientOptions())
        {
        }

        public MezonClient(MezonSocketClientOptions options) : this(options, CreateSocketClient(options))
        {
        }

        public MezonClient(MezonSocketClientOptions options, IMezonSocketClient socketClient) : base(options, socketClient)
        {
            _stateLock = new SemaphoreSlim(1, 1);
            _logger = LogManager.CreateLogger("MezonSocketClient");
            if (ApiClient is MezonSocketClient socketApiClient)
            {
                socketApiClient.ConfigureSocketLogging(LogManager);
            }

            _heartbeatTimes = new ConcurrentQueue<long>();
            HandlerTimeout = options.SocketHandlerTimeoutInMilliseconds;
            _connection = new SocketConnectionManager(
                _stateLock,
                _logger,
                options.ConnectionTimeoutInMilliseconds,
               OnConnectingAsync,
               OnDisconnectingAsync,
               x => ApiClient.SocketDisconnected += x);
            _connection.Connected += OnSocketConnectedAsync;
            _connection.Disconnected += ex => TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex);
            _connection.Reconnecting += ex => TimedInvokeAsync(_reconnectingEvent, nameof(Reconnecting), ex);
            ApiClient.SocketMessageSent += async msg => await _logger.DebugAsync(msg).ConfigureAwait(false);
            ApiClient.MessageReceived += ProcessMessageAsync;
        }

        private static MezonSocketClient CreateSocketClient(MezonSocketClientOptions options)
            => new MezonSocketClient(options.RestClientProvider, options.NetworkTransportProvider, options);

        private async Task OnConnectingAsync()
        {
            try
            {
                await _logger.DebugAsync("Connecting MezonSocket").ConfigureAwait(false);
                await ApiClient.ConnectAsync().ConfigureAwait(false);

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

        private async Task OnSocketConnectedAsync()
        {
            if (_heartbeatTask is { IsCompleted: false })
            {
                return;
            }

            _heartbeatTask = RunHeartbeatAsync(_connection.CancelToken);
            await TimedInvokeAsync(_connectedEvent, nameof(Connected)).ConfigureAwait(false);
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

        public async Task<Session> AuthenticateEmailAsync(string email, string password)
        {
            var res = await ApiClient.AuthenticateEmailAsync(Options.ServerKey, "", new EmailAuthenticationRequest
            {
                Account = new AccountEmailRequest
                {
                    Email = email,
                    Password = password,
                },
            }).ConfigureAwait(false);
            return new Session(res);
        }

        public async Task<LoginIDResponse> CreateQRLoginAsync(LoginRequest request)
        {
            var res = await ApiClient.CreateQRLoginAsync(Options.ServerKey, "", request).ConfigureAwait(false);
            return new LoginIDResponse
            {
                Address = res.Address,
                CreateTimeSeconds = res.CreateTimeSeconds,
                LoginId = res.LoginId,
                Platform = res.Platform,
                Status = res.Status,
                UserId = res.UserId,
                Username = res.Username,
            };
        }

        public async Task<ClanDescList> ListClanDescAsync(ListClanDescRequest request)
        {
            return await ApiClient.ListClanDescsAsync(request).ConfigureAwait(false);
        }

        public async Task<ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, int? channelType = null, bool? isMobile = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListChannelDescsRequest
            {
                ClanId = clanId
            };
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (state.HasValue)
            {
                request.State = state.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }
            if (channelType.HasValue)
            {
                request.ChannelType = channelType.Value;
            }
            if (isMobile.HasValue)
            {
                request.IsMobile = isMobile.Value;
            }
            if (page.HasValue)
            {
                request.Page = page.Value;
            }
            return await ApiClient.ListChannelDescsAsync(request, options).ConfigureAwait(false);
        }

        public async Task<RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new RoleListEventRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (state.HasValue)
            {
                request.State = state.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }
            return await ApiClient.ListRolesAsync(request, options).ConfigureAwait(false);
        }

        public async Task<RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListRoleUsersRequest();
            request.RoleId = roleId;
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }
            return await ApiClient.ListRoleUsersAsync(request, options).ConfigureAwait(false);
        }

        public Task<TResponse> SendSocketApiAsync<TRequest, TResponse>(
            string apiName,
            TRequest request,
            Google.Protobuf.MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TRequest : Google.Protobuf.IMessage<TRequest>
            where TResponse : Google.Protobuf.IMessage<TResponse>
        {
            if (ApiClient is MezonSocketClient socketClient)
            {
                return socketClient.SendApiAsync(apiName, request, responseParser, options);
            }

            throw new MezonConnectionException("Socket API requires a connected server.");
        }

        public Task<Mezon.Net.Internal.Realtime.Envelope> SendRealtimeAsync(Mezon.Net.Internal.Realtime.Envelope envelope, RequestOptions? options = null)
        {
            if (ApiClient is MezonSocketClient socketClient)
            {
                return socketClient.SendEnvelopeAsync(envelope, options);
            }

            throw new MezonConnectionException("Realtime socket requires MezonSocketApiClient.");
        }

        public Task<ClanDescList> GetClanDescriptionAsync(ListClanDescRequest request)
        {
            return ApiClient.ListClanDescsAsync(request);
        }

        #region INVOKE EVENT WITH HANDLER TIMEOUT
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
                    await _logger.WarningAsync($"A {name} handler is blocking the socket task.").ConfigureAwait(false);
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
    }
}
