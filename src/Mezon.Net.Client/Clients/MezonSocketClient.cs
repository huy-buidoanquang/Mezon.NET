using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mezon.Net.Abstractions;
using Mezon.Net.Client.Messaging;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Logging;
using Mezon.Net.Transport;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;
using MezonSession = Mezon.Net.Internal.Api.Session;

namespace Mezon.Net.Client
{
    internal class MezonSocketClient : MezonApiClient, IMezonSocketClient, IDisposable, IAsyncDisposable
    {
        private Logger? _logger;
        private readonly TransportType _transportType;
        private readonly SocketCorrelationHub _correlationHub = new();
        private long _lastPingSentMs;
        private long _lastPongReceivedMs;
        internal long LastPingSentMs => _lastPingSentMs;
        internal long LastPongReceivedMs => _lastPongReceivedMs;
        public event Func<string, Task> SocketMessageSent { add { _socketMessageSent.Add(value); } remove { _socketMessageSent.Remove(value); } }
        private readonly AsyncEvent<Func<string, Task>> _socketMessageSent = new AsyncEvent<Func<string, Task>>();

        public event Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task> MessageReceived { add { _messageReceived.Add(value); } remove { _messageReceived.Remove(value); } }
        private readonly AsyncEvent<Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task>> _messageReceived = new AsyncEvent<Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, Envelope?, Task>>();

        public event Func<Exception, Task> SocketDisconnected { add { _socketDisconnected.Add(value); } remove { _socketDisconnected.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _socketDisconnected = new AsyncEvent<Func<Exception, Task>>();

        private CancellationTokenSource? _connectCancelToken;

        internal IMezonNetworkTransporter NetworkTransporter { get; }

        public ConnectionState ConnectionState { get; private set; }

        public MezonSocketClient(RestClientProvider restClientProvider, MezonNetworkTransportProvider networkTransportProvider, MezonSocketClientOptions options)
            : base(restClientProvider, options)
        {
            _transportType = options.TransportType.Resolve();
            NetworkTransporter = networkTransportProvider(_transportType);
            RequestQueue.ConfigureTransportLimits(
                options.MaxTransportRequestsPerSecond,
                options.MaxTransportRequestsPerMinute,
                options.MaxConnectRequestsPerSecond);
            RequestQueue.SetDefaultRatelimitCallback(options.DefaultRatelimitCallback);
            NetworkTransporter.Opened += NetworkTransporter_Opened;
            NetworkTransporter.Closed += NetworkTransporter_Closed;
            NetworkTransporter.ErrorOccurred += NetworkTransporter_ErrorOccurred;
            NetworkTransporter.MessageReceived += NetworkTransporter_MessageReceived;
        }

        internal void ConfigureSocketLogging(LogManager logManager)
        {
            _logger = logManager.CreateLogger("MezonSocketApiClient");
        }

        private void LogTrace(string message)
        {
            if (_logger != null && _logger.Level == LogLevel.Trace)
            {
                _ = _logger.TraceAsync(message);
            }
        }

        public async Task ConnectAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        internal override async Task ConnectInternalAsync()
        {
            if (LoginState != LoginState.LoggedIn)
            {
                throw new MezonAuthenticationException("The client must be logged in before connecting.");
            }

            if (NetworkTransporter == null)
            {
                throw new NotSupportedException("This client is not configured with WebSocket support.");
            }

            RequestQueue.ResetTransportLimits();

            ConnectionState = ConnectionState.Connecting;
            RequestQueue.BeginConnectPhase();
            try
            {
                _connectCancelToken?.Dispose();
                _connectCancelToken = new CancellationTokenSource();
                NetworkTransporter.SetCancelToken(_connectCancelToken.Token);
                var socketOptions = (MezonSocketClientOptions)MezonOptions;
                var (host, port, token) = GetTransportEndpoint();
                await NetworkTransporter.ConnectAsync(host, port, token, useSsl: true, createStatus: socketOptions.CreateStatusOnConnect).ConfigureAwait(false);
                ConnectionState = ConnectionState.Connected;
            }
            catch
            {
                await DisconnectInternalAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task DisconnectAsync(Exception? ex = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(ex).ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        internal override async Task DisconnectInternalAsync(Exception? ex = null)
        {
            if (NetworkTransporter == null)
            {
                throw new NotSupportedException("This client is not configured with Socket support.");
            }

            if (ConnectionState == ConnectionState.Disconnected)
            {
                return;
            }

            ConnectionState = ConnectionState.Disconnecting;
            _correlationHub.FailAll(new OperationCanceledException(ex?.Message ?? "Socket disconnected."));
            await NetworkTransporter.DisconnectAsync().ConfigureAwait(false);
            try
            {
                _connectCancelToken?.Cancel(false);
            }
            catch { }

            ConnectionState = ConnectionState.Disconnected;
        }

        private static byte[] SerializeMessage(IMessage message)
        {
            var size = message.CalculateSize();
            if (size == 0)
            {
                return Array.Empty<byte>();
            }

            var rented = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                message.WriteTo(new Span<byte>(rented, 0, size));
                var payload = new byte[size];
                Buffer.BlockCopy(rented, 0, payload, 0, size);
                return payload;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        internal async Task Heartbeat(RequestOptions? options = null)
        {
            _lastPingSentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _correlationHub.AllocateCid();
            var timeout = options.SocketSendTimeout ?? MezonOptions.SocketTimeoutInMilliseconds;
            var pendingRequest = _correlationHub.Register(cid, options.CancelToken);
            LogTrace($"[SOCKET-SEND] heartbeat cid={cid} timeout={timeout}ms");

            try
            {
                if (_transportType == TransportType.WebSocket)
                {
                    var envelope = new Envelope { Cid = cid, Ping = new Ping() };
                    await SendSocketInternalAsync(MezonMessageType.Realtime, cid, SerializeMessage(envelope), options, bypassRateLimiter: true).ConfigureAwait(false);
                }
                else
                {
                    await SendSocketInternalAsync(MezonMessageType.Heartbeat, cid, Array.Empty<byte>(), options, bypassRateLimiter: true).ConfigureAwait(false);
                }

                pendingRequest.StartTimeout(timeout);
                await pendingRequest.Task.ConfigureAwait(false);
            }
            catch
            {
                pendingRequest.Abort(new OperationCanceledException("Heartbeat send failed before a response was received."));
                NetworkTransporter.RemoveApiChunkBuffer(cid);
                throw;
            }
        }

        private (string host, int port, string token) GetTransportEndpoint()
        {
            var session = SessionManager<MezonApiClientOptions>.Instance.CurrentSession();
            var connectToken = !string.IsNullOrEmpty(session.SessionId)
                ? session.SessionId
                : session.AuthToken ?? string.Empty;
            var endpointUrl = _transportType == TransportType.Tcp || _transportType == TransportType.Auto ? session.TcpUrl : session.WsUrl;

            if (endpointUrl == null)
            {
                return (string.Empty, 0, connectToken);
            }

            var parts = endpointUrl.Split(':');
            if (parts == null)
            {
                return (string.Empty, 0, connectToken);
            }

            if (parts.Length >= 2 && int.TryParse(parts[^1], out var port))
            {
                return (parts[0], port, connectToken);
            }

            return (string.Empty, 0, connectToken);
        }

        #region Event Handlers
        private Task NetworkTransporter_Opened()
        {
            return Task.CompletedTask;
        }

        private Task NetworkTransporter_ErrorOccurred(Exception exception)
        {
            if (_logger != null)
            {
                return _logger.WarningAsync($"Transport error: {exception.Message}");
            }

            return Task.CompletedTask;
        }

        private Task NetworkTransporter_Closed(Exception? exception)
        {
            if (ConnectionState == ConnectionState.Disconnected)
            {
                if (!_socketDisconnected.HasSubscribers)
                {
                    return Task.CompletedTask;
                }

                return _socketDisconnected.InvokeAsync(exception ?? new Exception("Socket closed."));
            }

            ConnectionState = ConnectionState.Disconnecting;
            try
            {
                _connectCancelToken?.Cancel(false);
            }
            catch
            {
            }

            ConnectionState = ConnectionState.Disconnected;
            if (!_socketDisconnected.HasSubscribers)
            {
                return Task.CompletedTask;
            }

            return _socketDisconnected.InvokeAsync(exception ?? new Exception("Socket closed."));
        }

        private ValueTask NetworkTransporter_MessageReceived(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0 && type is not MezonMessageType.Heartbeat and not MezonMessageType.Api)
            {
                return default;
            }

            try
            {
                if (type == MezonMessageType.Heartbeat)
                {
                    if (cid == -1)
                    {
                        return default;
                    }

                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (_lastPingSentMs > 0)
                    {
                        LatencyMilliseconds = (int)Math.Max(0, now - _lastPingSentMs);
                    }

                    _lastPongReceivedMs = now;
                    var matched = _correlationHub.TryComplete(cid, code, ReadOnlyMemory<byte>.Empty);
                    LogTrace($"[SOCKET-RECEIVE] type={type} cid={cid} code={code} bytes={data.Length} pending={_correlationHub.PendingCount}");
                    return default;
                }

                if (type == MezonMessageType.Api)
                {
                    if (cid == -1 || code == -1)
                    {
                        return default;
                    }

                    var matched = _correlationHub.TryComplete(cid, code, data);
                    LogTrace($"[SOCKET-RECEIVE] type={type} cid={cid} code={code} bytes={data.Length} pending={_correlationHub.PendingCount}");
                    return default;
                }

                if (type == MezonMessageType.Realtime)
                {
                    var envelope = Envelope.Parser.ParseFrom(data.Span);
                    if (envelope.Cid > 0)
                    {
                        cid = envelope.Cid;
                        _correlationHub.TryComplete(envelope.Cid, code, SerializeMessage(envelope));
                    }

                    if (_messageReceived.HasSubscribers)
                    {
                        _ = _messageReceived.InvokeAsync(type, cid, code, data, envelope);
                    }

                    LogTrace($"[SOCKET-RECEIVE] type={type} cid={envelope.Cid} code={code} bytes={data.Length} pending={_correlationHub.PendingCount} env={envelope.MessageCase}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                LogTrace($"[SOCKET-RECEIVE] parse error type={type} cid={cid}: {ex.Message}");
            }

#if NET6_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask();
#endif
        }

        #endregion

        internal override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _connectCancelToken?.Dispose();
                    (NetworkTransporter as IDisposable)?.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        internal override ValueTask DisposeAsync(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _connectCancelToken?.Dispose();
                    (NetworkTransporter as IDisposable)?.Dispose();
                }
            }

            return base.DisposeAsync(disposing);
        }

        #region Core
        public Task<TResponse> SendSocketApiAsync<TRequest, TResponse>(string apiName, TRequest request, MessageParser<TResponse> responseParser, RequestOptions? options = null) where TRequest : IMessage<TRequest> where TResponse : IMessage<TResponse>
        {
            if (!MezonApiMap.TryGetIndex(apiName, out var apiIndex))
            {
                throw new ArgumentException($"Unknown socket API name '{apiName}'.", nameof(apiName));
            }

            var envelope = new Envelope
            {
                ApiRequestEvent = new ApiRequestEvent
                {
                    ApiIndex = apiIndex,
                    ApiName = apiName,
                    Body = request.ToByteString(),
                }
            };

            return SendSocketApiInternalAsync(envelope, responseParser, options);
        }

        private async Task<TResponse> SendSocketApiInternalAsync<TResponse>(Envelope envelope, MessageParser<TResponse> responseParser, RequestOptions? options = null) where TResponse : IMessage<TResponse>
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _correlationHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? MezonOptions.SocketTimeoutInMilliseconds;
            var pendingRequest = _correlationHub.Register(cid, options.CancelToken);
            var payload = SerializeMessage(envelope);
            LogTrace($"[SOCKET-SEND] api={envelope.ApiRequestEvent.ApiName} cid={cid} bytes={payload.Length} timeout={timeout}ms");

            try
            {
                await SendSocketInternalAsync(MezonMessageType.Api, cid, payload, options).ConfigureAwait(false);
                pendingRequest.StartTimeout(timeout);
                var socketResponse = await pendingRequest.Task.ConfigureAwait(false);

                if (socketResponse.Code != 0)
                {
                    throw MezonApiException.FromSocketResponse(socketResponse.Code, envelope.ApiRequestEvent?.ApiName, socketResponse.Payload);
                }

                return responseParser.ParseFrom(socketResponse.Payload.Span);
            }
            catch
            {
                pendingRequest.Abort(new OperationCanceledException("Socket API send failed before a response was received."));
                NetworkTransporter.RemoveApiChunkBuffer(cid);
                throw;
            }
        }

        internal async Task SendRtInternalAsync(Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();
            envelope.Cid = _correlationHub.AllocateCid();
            var payload = SerializeMessage(envelope);
            await SendSocketInternalAsync(MezonMessageType.Realtime, envelope.Cid, payload, options).ConfigureAwait(false);
        }

        internal async Task<Envelope> SendRtInternalAwaitResponseAsync(Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _correlationHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? MezonOptions.SocketTimeoutInMilliseconds;
            var pendingRequest = _correlationHub.Register(cid, options.CancelToken);
            var payload = SerializeMessage(envelope);
            try
            {
                LogTrace($"[SOCKET-SEND] rt-await-ack env={envelope.MessageCase} cid={cid} timeout={timeout}ms");
                await SendSocketInternalAsync(MezonMessageType.Realtime, cid, payload, options).ConfigureAwait(false);
                pendingRequest.StartTimeout(timeout);
                var socketResponse = await pendingRequest.Task.ConfigureAwait(false);

                if (socketResponse.Code != 0)
                {
                    throw MezonApiException.FromSocketResponse(socketResponse.Code, envelope.ApiRequestEvent?.ApiName, socketResponse.Payload);
                }

                if (socketResponse.Payload.Length > 0)
                {
                    return Envelope.Parser.ParseFrom(socketResponse.Payload.Span);
                }

                return envelope;
            }
            catch
            {
                pendingRequest.Abort(new OperationCanceledException("Realtime send failed before an acknowledgement was received."));
                NetworkTransporter.RemoveApiChunkBuffer(cid);
                throw;
            }
        }

        internal async Task SendSocketInternalAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data, RequestOptions options, bool bypassRateLimiter = false)
        {
            if (!bypassRateLimiter)
            {
                await RequestQueue.EnterTransportAsync(options).ConfigureAwait(false);
            }

            await NetworkTransporter.SendAsync(type, cid, data).ConfigureAwait(false);
        }
        #endregion

        #region Socket API

        public int LatencyMilliseconds { get; private set; }

        internal int PendingSocketRequestCount => _correlationHub.PendingCount;

        public override Task<ClanDescList> ListClanDescsAsync(ListClanDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("ListClanDescs", body, ClanDescList.Parser, options);
        }

        public override Task<MezonSession> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, global::Mezon.Net.Internal.Api.SessionRefreshRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            return SendSocketApiAsync("SessionRefresh", body, MezonSession.Parser, options);
        }

        public override async Task DeleteAccountAsync(RequestOptions? options = null)
        {
            await SendSocketApiAsync("DeleteAccount", new Empty(), Empty.Parser, options);
        }

        public override Task<Account> GetAccountAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetAccount", new Empty(), Account.Parser, options);
        }

        public override Task<AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new Internal.Api.AddFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }
            return SendSocketApiAsync("AddFriends", request, AddFriendsResponse.Parser, options);
        }

        public override async Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new BlockFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }
            await SendSocketApiAsync("BlockFriends", request, Empty.Parser, options);
        }

        public override async Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new BlockFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }
            await SendSocketApiAsync("UnblockFriends", request, Empty.Parser, options);
        }

        public override async Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new DeleteFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }
            await SendSocketApiAsync("DeleteFriends", request, Empty.Parser, options);
        }

        public override Task<FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListFriendsRequest();
            if (state.HasValue)
            {
                request.State = state.Value;
            }
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }
            return SendSocketApiAsync("ListFriends", request, FriendList.Parser, options);
        }

        public override Task<ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null)
        {
            Check.NotNullOrEmpty(clanName, nameof(clanName));
            var request = new CreateClanDescRequest();
            request.ClanName = clanName;
            if (!string.IsNullOrEmpty(logo))
            {
                request.Logo = logo;
            }
            if (!string.IsNullOrEmpty(banner))
            {
                request.Banner = banner;
            }
            return SendSocketApiAsync("CreateClanDesc", request, ClanDesc.Parser, options);
        }

        public override async Task DeleteClanDescAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteClanDescRequest();
            request.ClanDescId = clanId;
            await SendSocketApiAsync("DeleteClanDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateClanDesc", body, Empty.Parser, options);
        }

        public override Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanUsersRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListClanUsers", request, ClanUserList.Parser, options);
        }

        public override async Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new RemoveClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            await SendSocketApiAsync("RemoveClanUsers", request, Empty.Parser, options);
        }

        public override async Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new BanClanUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            if (banTime.HasValue)
            {
                request.BanTime = banTime.Value;
            }
            if (!string.IsNullOrEmpty(reason))
            {
                request.Reason = reason;
            }
            await SendSocketApiAsync("BanClanUsers", request, Empty.Parser, options);
        }

        public override Task<Internal.Api.ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateChannelDesc", body, Internal.Api.ChannelDescription.Parser, options);
        }

        public override async Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            request.ClanId = 2050100607154393088;
            await SendSocketApiAsync("DeleteChannelDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateChannelDesc", body, Empty.Parser, options);
        }

        public override async Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new AddChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            await SendSocketApiAsync("AddChannelUsers", request, Empty.Parser, options);
        }

        public override async Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new RemoveChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            await SendSocketApiAsync("RemoveChannelUsers", request, Empty.Parser, options);
        }

        public override Task<ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null)
        {
            var request = new ListChannelMessagesRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            if (messageId.HasValue)
            {
                request.MessageId = messageId.Value;
            }
            if (direction.HasValue)
            {
                request.Direction = direction.Value;
            }
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (topicId.HasValue)
            {
                request.TopicId = topicId.Value;
            }
            return SendSocketApiAsync("ListChannelMessages", request, ChannelMessageList.Parser, options);
        }

        public override Task<ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
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
            return SendSocketApiAsync("ListChannelUsers", request, ChannelUserList.Parser, options);
        }

        public override async Task DeleteRoleAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendSocketApiAsync("DeleteRole", request, Empty.Parser, options);
        }

        public override Task<RoleListEventResponse> ListRolesAsync(RoleListEventRequest request, RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListRoles", request, RoleListEventResponse.Parser, options);
        }

        public override async Task UpdateUserAsync(UpdateUsersRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateUser", body, Empty.Parser, options);
        }

        public override async Task DeleteEventAsync(long eventId, RequestOptions? options = null)
        {
            var request = new DeleteEventRequest();
            request.EventId = eventId;
            await SendSocketApiAsync("DeleteEvent", request, Empty.Parser, options);
        }

        public override Task<EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListEventsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }
            return SendSocketApiAsync("ListEvents", request, EventList.Parser, options);
        }

        public override Task<ChannelMessage> CreatePinMessageAsync(PinMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreatePinMessage", body, ChannelMessage.Parser, options);
        }

        public override Task<PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new PinMessageRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendSocketApiAsync("GetPinMessagesList", request, PinMessagesList.Parser, options);
        }

        public override async Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new DeletePinMessage();
            request.MessageId = messageId;
            request.ChannelId = channelId;
            request.ClanId = clanId;
            await SendSocketApiAsync("DeletePinMessage", request, Empty.Parser, options);
        }

        public override async Task MarkAsReadAsync(MarkAsReadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("MarkAsRead", body, Empty.Parser, options);
        }

        public override async Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("CreateClanEmoji", body, Empty.Parser, options);
        }

        public override async Task UpdateClanEmojiByIdAsync(ClanEmojiUpdateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateClanEmojiById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanEmojiDeleteRequest();
            request.Id = emojiId;
            request.ClanId = clanId;
            await SendSocketApiAsync("DeleteByIdClanEmoji", request, Empty.Parser, options);
        }

        public override async Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("AddClanSticker", body, Empty.Parser, options);
        }

        public override async Task UpdateClanStickerByIdAsync(ClanStickerUpdateByIdRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateClanStickerById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanStickerDeleteRequest();
            request.Id = stickerId;
            request.ClanId = clanId;
            await SendSocketApiAsync("DeleteClanStickerById", request, Empty.Parser, options);
        }

        public override Task<EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetListEmojisByUserId", new Empty(), EmojiListedResponse.Parser, options);
        }

        public override Task<StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetListStickersByUserId", new Empty(), StickerListedResponse.Parser, options);
        }

        public override Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GenerateWebhook", body, WebhookGenerateResponse.Parser, options);
        }

        public override Task<WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new WebhookListRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendSocketApiAsync("ListWebhookByChannelId", request, WebhookListResponse.Parser, options);
        }

        public override async Task UpdateWebhookByIdAsync(WebhookUpdateRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateWebhookById", body, Empty.Parser, options);
        }

        public override async Task DeleteWebhookByIdAsync(WebhookDeleteRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteWebhookById", body, Empty.Parser, options);
        }

        public override async Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("CreateSystemMessage", body, Empty.Parser, options);
        }

        public override async Task UpdateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateSystemMessage", body, Empty.Parser, options);
        }

        public override Task<SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null)
        {
            var request = new GetSystemMessage();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetSystemMessageByClanId", request, SystemMessage.Parser, options);
        }

        public override async Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteSystemMessage();
            request.ClanId = clanId;
            await SendSocketApiAsync("DeleteSystemMessage", request, Empty.Parser, options);
        }

        public override async Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateRoleOrder", body, Empty.Parser, options);
        }

        public override async Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateClanOrder", body, Empty.Parser, options);
        }

        public override Task<ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChanEncryptionMethod();
            request.ChannelId = channelId;
            return SendSocketApiAsync("GetChanEncryptionMethod", request, ChanEncryptionMethod.Parser, options);
        }

        public override async Task SetChanEncryptionMethodAsync(ChanEncryptionMethod body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetChanEncryptionMethod", body, Empty.Parser, options);
        }

        public override Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new GetPubKeysRequest();
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            return SendSocketApiAsync("GetPubKeys", request, GetPubKeysResponse.Parser, options);
        }

        public override async Task PushPublicKeyAsync(PushPubKeyRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("PushPubKey", body, Empty.Parser, options);
        }

        public override Task<GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetKeyServer", new Empty(), GetKeyServerResp.Parser, options);
        }

        public override Task<ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null)
        {
            var request = new ListOnboardingRequest();
            request.ClanId = clanId;
            if (guideType.HasValue)
            {
                request.GuideType = guideType.Value;
            }
            return SendSocketApiAsync("ListOnboarding", request, ListOnboardingResponse.Parser, options);
        }

        public override Task<OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            return SendSocketApiAsync("GetOnboardingDetail", request, OnboardingItem.Parser, options);
        }

        public override Task<ListOnboardingResponse> CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateOnboarding", body, ListOnboardingResponse.Parser, options);
        }

        public override async Task UpdateOnboardingAsync(UpdateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateOnboarding", body, Empty.Parser, options);
        }

        public override async Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            await SendSocketApiAsync("DeleteOnboarding", request, Empty.Parser, options);
        }

        public override Task<ListUserActivity> ListActivityAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListActivity", new Empty(), ListUserActivity.Parser, options);
        }

        public override Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GenerateMeetToken", body, GenerateMeetTokenResponse.Parser, options);
        }

        public override async Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("TransferOwnership", body, Empty.Parser, options);
        }

        public override Task<PermissionList> GetListPermissionAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetListPermission", new Empty(), PermissionList.Parser, options);
        }

        public override Task<PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null)
        {
            var request = new ListPermissionsRequest();
            request.RoleId = roleId;
            return SendSocketApiAsync("ListRolePermissions", request, PermissionList.Parser, options);
        }

        public override Task<RoleUserList> ListRoleUsersAsync(ListRoleUsersRequest request, RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListRoleUsers", request, RoleUserList.Parser, options);
        }

        public override Task<UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null)
        {
            var request = new UserPermissionInChannelListRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            return SendSocketApiAsync("ListUserPermissionInChannel", request, UserPermissionInChannelListResponse.Parser, options);
        }

        public override async Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null)
        {
            var request = new DeleteNotificationsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (category.HasValue)
            {
                request.Category = category.Value;
            }
            await SendSocketApiAsync("DeleteNotifications", request, Empty.Parser, options);
        }

        public override Task<NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? category = null, int? direction = null, RequestOptions? options = null)
        {
            var request = new ListNotificationsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }
            if (notificationId.HasValue)
            {
                request.NotificationId = notificationId.Value;
            }
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (category.HasValue)
            {
                request.Category = category.Value;
            }
            if (direction.HasValue)
            {
                request.Direction = direction.Value;
            }
            return SendSocketApiAsync("ListNotifications", request, NotificationList.Parser, options);
        }

        public override Task<CategoryDesc> CreateCategoryDescAsync(CreateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateCategoryDesc", body, CategoryDesc.Parser, options);
        }

        public override async Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null)
        {
            var request = new DeleteCategoryDescRequest();
            request.CategoryId = categoryId;
            request.ClanId = clanId;
            await SendSocketApiAsync("DeleteCategoryDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateCategoryAsync(UpdateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateCategory", body, Empty.Parser, options);
        }

        public override Task<CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new CategoryDesc();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListCategoryDescs", request, CategoryDescList.Parser, options);
        }

        public override Task<InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null)
        {
            var request = new InviteUserRequest();
            request.InviteId = inviteId;
            return SendSocketApiAsync("InviteUser", request, InviteUserRes.Parser, options);
        }

        public override async Task SetNotificationChannelSettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetNotificationChannelSetting", body, Empty.Parser, options);
        }

        public override async Task SetMuteNotificationCategoryAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetMuteCategory", body, Empty.Parser, options);
        }

        public override async Task SetMuteNotificationChannelAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetMuteChannel", body, Empty.Parser, options);
        }

        public override Task<NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetChannelCategoryNotiSettingsList", request, NotificationChannelCategorySettingList.Parser, options);
        }

        public override Task<NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetNotificationClan", request, NotificationSetting.Parser, options);
        }

        public override Task<UserStatus> GetUserStatusAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("GetUserStatus", new Empty(), UserStatus.Parser, options);
        }

        public override async Task UpdateUserStatusAsync(UserStatusUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateUserStatus", body, Empty.Parser, options);
        }

        public override Task<AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListAppsRequest();
            if (!string.IsNullOrEmpty(filter))
            {
                request.Filter = filter;
            }
            if (tombstones.HasValue)
            {
                request.Tombstones = tombstones.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }
            return SendSocketApiAsync("ListApps", request, AppList.Parser, options);
        }

        public override Task<App> GetAppAsync(long id, RequestOptions? options = null)
        {
            var request = new AppId();
            request.Id = id;
            return SendSocketApiAsync("GetApp", request, App.Parser, options);
        }

        public override Task<App> UpdateAppAsync(UpdateAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UpdateApp", body, App.Parser, options);
        }

        public override async Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null)
        {
            var request = new AppDeleteRequest();
            request.Id = id;
            if (recordDeletion.HasValue)
            {
                request.RecordDeletion = recordDeletion.Value;
            }
            await SendSocketApiAsync("DeleteApp", request, Empty.Parser, options);
        }

        public override async Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null)
        {
            var request = new AppClan();
            request.AppId = appId;
            request.ClanId = clanId;
            await SendSocketApiAsync("AddAppToClan", request, Empty.Parser, options);
        }

        public override Task<ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null)
        {
            var request = new ListAuditLogRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }
            if (!string.IsNullOrEmpty(actionLog))
            {
                request.ActionLog = actionLog;
            }
            if (userId.HasValue)
            {
                request.UserId = userId.Value;
            }
            if (!string.IsNullOrEmpty(dateLog))
            {
                request.DateLog = dateLog;
            }
            return SendSocketApiAsync("ListAuditLog", request, ListAuditLog.Parser, options);
        }

        public override async Task AddUserEventAsync(UserEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("AddUserEvent", body, Empty.Parser, options);
        }

        public override async Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null)
        {
            var request = new UserEventRequest();
            request.ClanId = clanId;
            request.EventId = eventId;
            await SendSocketApiAsync("DeleteUserEvent", request, Empty.Parser, options);
        }

        public override async Task HealthcheckAsync(RequestOptions? options = null)
        {
            await SendSocketApiAsync("Healthcheck", new Empty(), Empty.Parser, options);
        }

        public override Task<ChannelDescList> ListChannelDescsAsync(ListChannelDescsRequest request, RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListChannelDescs", request, ChannelDescList.Parser, options);
        }

        public override Task<Internal.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelDetailRequest();
            request.ChannelId = channelId;
            return SendSocketApiAsync("ListChannelDetail", request, Internal.Api.ChannelDescription.Parser, options);
        }

        public override Task<BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new BannedUserListRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListBannedUsers", request, BannedUserList.Parser, options);
        }

        public override async Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new BanClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }
            await SendSocketApiAsync("UnbanClanUsers", request, Empty.Parser, options);
        }

        public override Task<RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(RegistFcmDeviceTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("RegistFCMDeviceToken", body, RegistFcmDeviceTokenResponse.Parser, options);
        }

        public override Task<AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListUserClansByUserId", new Empty(), AllUserClans.Parser, options);
        }

        public override Task<ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListChannelAppsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }
            return SendSocketApiAsync("ListChannelApps", request, ListChannelAppsResponse.Parser, options);
        }

        public override async Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendSocketApiAsync("CloseDMByChannelId", request, Empty.Parser, options);
        }

        public override async Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendSocketApiAsync("OpenDMByChannelId", request, Empty.Parser, options);
        }

        public override Task<ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ClanProfileRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetUserProfileOnClan", request, ClanProfile.Parser, options);
        }

        public override async Task UpdateUserProfileByClanAsync(UpdateClanProfileRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateUserProfileByClan", body, Empty.Parser, options);
        }

        public override async Task LeaveThreadAsync(long channelId, RequestOptions? options = null)
        {
            var request = new LeaveThreadRequest();
            request.ChannelId = channelId;
            await SendSocketApiAsync("LeaveThread", request, Empty.Parser, options);
        }

        public override Task<ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListThreadRequest();
            request.ChannelId = channelId;
            return SendSocketApiAsync("ListThreadDescs", request, ChannelDescListNoPool.Parser, options);
        }

        public override Task<ChannelDescList> SearchThreadAsync(SearchThreadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("SearchThread", body, ChannelDescList.Parser, options);
        }

        public override Task<LinkAccountConfirmRequest> LinkSMSAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("LinkSMS", body, LinkAccountConfirmRequest.Parser, options);
        }

        public override async Task ConfirmLinkMezonOTPAsync(LinkAccountConfirmRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ConfirmLinkMezonOTP", body, Empty.Parser, options);
        }

        public override Task<LinkAccountConfirmRequest> LinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("LinkEmail", body, LinkAccountConfirmRequest.Parser, options);
        }

        public override async Task UnlinkMezonAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UnlinkMezon", body, Empty.Parser, options);
        }

        public override async Task UnlinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UnlinkEmail", body, Empty.Parser, options);
        }

        public override Task<IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null)
        {
            var request = new IsBannedRequest();
            request.ChannelId = channelId;
            return SendSocketApiAsync("IsBanned", request, IsBannedResponse.Parser, options);
        }

        public override async Task AddRolesChannelDescAsync(AddRoleChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("AddRolesChannelDesc", body, Empty.Parser, options);
        }

        public override async Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendSocketApiAsync("DeleteRoleChannelDesc", request, Empty.Parser, options);
        }

        public override async Task SetRoleChannelPermissionAsync(UpdateRoleChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetRoleChannelPermission", body, Empty.Parser, options);
        }

        public override Task<RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListPermissionOfUsersRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetRoleOfUserInTheClan", request, RoleList.Parser, options);
        }

        public override Task<PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(PermissionRoleChannelListEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetPermissionByRoleIdChannelId", body, PermissionRoleChannelListEventResponse.Parser, options);
        }

        public override Task<ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelAttachmentRequest();
            request.ChannelId = channelId;
            return SendSocketApiAsync("ListChannelAttachment", request, ChannelAttachmentList.Parser, options);
        }

        public override Task<VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendSocketApiAsync("ListChannelVoiceUsers", request, VoiceChannelUserList.Parser, options);
        }

        public override Task<StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendSocketApiAsync("ListStreamingChannelUsers", request, StreamingChannelUserList.Parser, options);
        }

        public override Task<ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("ListChannelByUserId", new Empty(), ChannelDescListNoPool.Parser, options);
        }

        public override Task<NotificationUserChannel> GetNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetNotificationChannel", body, NotificationUserChannel.Parser, options);
        }

        public override Task<NotificationUserChannel> GetNotificationCategoryAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetNotificationCategory", body, NotificationUserChannel.Parser, options);
        }

        public override async Task SetNotificationCategorySettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetNotificationCategorySetting", body, Empty.Parser, options);
        }

        public override async Task DeleteNotificationCategorySettingAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteNotificationCategorySetting", body, Empty.Parser, options);
        }

        public override async Task DeleteNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteNotificationChannel", body, Empty.Parser, options);
        }

        public override Task<ChannelMessage> CreateMessage2InboxAsync(Message2InboxRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateMessage2Inbox", body, ChannelMessage.Parser, options);
        }

        public override Task<ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ChannelSettingListRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListChannelSetting", request, ChannelSettingListResponse.Parser, options);
        }

        public override async Task UpdateChannelPrivateAsync(ChangeChannelPrivateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateChannelPrivate", body, Empty.Parser, options);
        }

        public override async Task ChangeChannelCategoryAsync(ChangeChannelCategoryRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ChangeChannelCategory", body, Empty.Parser, options);
        }

        public override Task<EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null)
        {
            return SendSocketApiAsync("EmojiRecentList", new Empty(), EmojiRecentList.Parser, options);
        }

        public override Task<AllUsersAddChannelResponse> ListChannelUsersUCAsync(AllUsersAddChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("ListChannelUsersUC", body, AllUsersAddChannelResponse.Parser, options);
        }

        public override Task<EditChannelCanvasResponse> EditChannelCanvasesAsync(EditChannelCanvasRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("EditChannelCanvases", body, EditChannelCanvasResponse.Parser, options);
        }

        public override Task<ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChannelCanvasListRequest();
            request.ChannelId = channelId;
            return SendSocketApiAsync("GetChannelCanvasList", request, ChannelCanvasListResponse.Parser, options);
        }

        public override Task<ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null)
        {
            var request = new ChannelCanvasDetailRequest();
            request.Id = id;
            return SendSocketApiAsync("GetChannelCanvasDetail", request, ChannelCanvasDetailResponse.Parser, options);
        }

        public override async Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null)
        {
            var request = new DeleteChannelCanvasRequest();
            request.CanvasId = canvasId;
            await SendSocketApiAsync("DeleteChannelCanvas", request, Empty.Parser, options);
        }

        public override Task<ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListFavoriteChannelRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("GetListFavoriteChannel", request, ListFavoriteChannelResponse.Parser, options);
        }

        public override Task<AddFavoriteChannelResponse> AddChannelFavoriteAsync(AddFavoriteChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("AddChannelFavorite", body, AddFavoriteChannelResponse.Parser, options);
        }

        public override async Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null)
        {
            var request = new RemoveFavoriteChannelRequest();
            request.ChannelId = channelId;
            await SendSocketApiAsync("RemoveChannelFavorite", request, Empty.Parser, options);
        }

        public override Task<GenerateClanWebhookResponse> GenerateClanWebhookAsync(GenerateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GenerateClanWebhook", body, GenerateClanWebhookResponse.Parser, options);
        }

        public override Task<ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanWebhookRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListClanWebhook", request, ListClanWebhookResponse.Parser, options);
        }

        public override async Task UpdateClanWebhookByIdAsync(UpdateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateClanWebhookById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null)
        {
            var request = new ClanWebhookRequest();
            request.Id = id;
            await SendSocketApiAsync("DeleteClanWebhookById", request, Empty.Parser, options);
        }

        public override Task<ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListOnboardingStepRequest();
            request.ClanId = clanId;
            return SendSocketApiAsync("ListOnboardingStep", request, ListOnboardingStepResponse.Parser, options);
        }

        public override async Task UpdateOnboardingStepAsync(UpdateOnboardingStepRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateOnboardingStep", body, Empty.Parser, options);
        }

        public override async Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteQuickMenuAccess", body, Empty.Parser, options);
        }

        public override async Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("AddQuickMenuAccess", body, Empty.Parser, options);
        }

        public override async Task UpdateQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateQuickMenuAccess", body, Empty.Parser, options);
        }

        public override Task<QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null)
        {
            var request = new ListQuickMenuAccessRequest();
            request.BotId = botId;
            request.ChannelId = channelId;
            if (menuType.HasValue)
            {
                request.MenuType = menuType.Value;
            }
            return SendSocketApiAsync("ListQuickMenuAccess", request, QuickMenuAccessList.Parser, options);
        }

        public override Task<IsFollowerResponse> IsFollowerAsync(IsFollowerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("IsFollower", body, IsFollowerResponse.Parser, options);
        }

        public override Task<ChannelMessageAck> SendChannelMessageAsync(ChannelMessageSend body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("SendChannelMessage", body, ChannelMessageAck.Parser, options);
        }

        public override Task<ChannelMessageAck> SendChannelMessageAsync(Mezon.Net.Models.SendChannelMessageParams message, RequestOptions? options = null)
            => SendChannelMessageAsync(MessageSendHelper.ToChannelMessageSend(message), options);

        public override async Task UpdateChannelMessageAsync(ChannelMessageUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateChannelMessage", body, Empty.Parser, options);
        }

        public override async Task DeleteChannelMessageAsync(ChannelMessageRemove body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteChannelMessage", body, Empty.Parser, options);
        }

        public override async Task RemoveParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("RemoveParticipantMezonMeet", body, Empty.Parser, options);
        }

        public override async Task MuteParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("MuteParticipantMezonMeet", body, Empty.Parser, options);
        }

        public override Task<CreateRoomChannelApps> CreateRoomChannelAppsAsync(CreateRoomChannelApps body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateRoomChannelApps", body, CreateRoomChannelApps.Parser, options);
        }

        public override Task<GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(GenerateHashChannelAppsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GenerateHashChannelApps", body, GenerateHashChannelAppsResponse.Parser, options);
        }

        public override Task<MezonOauthClient> GetMezonOauthClientAsync(GetMezonOauthClientRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public override async Task DeleteMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteMezonOauthClient", body, Empty.Parser, options);
        }

        public override Task<MezonOauthClient> UpdateMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UpdateMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public override Task<SdTopicList> ListSdTopicAsync(ListSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("ListSdTopic", body, SdTopicList.Parser, options);
        }

        public override Task<SdTopic> GetTopicDetailAsync(SdTopicDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetTopicDetail", body, SdTopic.Parser, options);
        }

        public override Task<SdTopic> CreateSdTopicAsync(SdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateSdTopic", body, SdTopic.Parser, options);
        }

        public override async Task DeleteSdTopicAsync(DeleteSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DeleteSdTopic", body, Empty.Parser, options);
        }

        public override async Task MessageButtonClickAsync(MessageButtonClicked body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("MessageButtonClick", body, Empty.Parser, options);
        }

        public override async Task DropdownBoxSelectedAsync(DropdownBoxSelected body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DropdownBoxSelected", body, Empty.Parser, options);
        }

        public override async Task ActiveArchivedThreadAsync(ActiveArchivedThread body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ActiveArchivedThread", body, Empty.Parser, options);
        }

        public override async Task AddAgentToChannelAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("AddAgentToChannel", body, Empty.Parser, options);
        }

        public override async Task DisconnectAgentAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("DisconnectAgent", body, Empty.Parser, options);
        }

        public override async Task ReportMessageAbuseAsync(ReportMessageAbuseReqest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ReportMessageAbuse", body, Empty.Parser, options);
        }

        public override Task<StreamHttpCallbackResponse> StreamingServerCallbackAsync(StreamHttpCallbackRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("StreamingServerCallback", body, StreamHttpCallbackResponse.Parser, options);
        }

        public override Task<ForSaleItemList> ListForSaleItemsAsync(ListForSaleItemsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("ListForSaleItems", body, ForSaleItemList.Parser, options);
        }

        public override async Task HandleClanWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("HandleClanWebhook", body, Empty.Parser, options);
        }

        public override Task<MutedChannelList> ListMutedChannelAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListMutedChannelRequest { ClanId = clanId };
            return SendSocketApiAsync("ListMutedChannel", request, MutedChannelList.Parser, options);
        }

        public override Task<ListClanBadgeCountResponse> ListClanBadgeCountAsync(RequestOptions? options = null)
            => SendSocketApiAsync("ListClanBadgeCount", new NoParams(), ListClanBadgeCountResponse.Parser, options);

        public override Task<ListChannelBadgeCountResponse> ListChannelBadgeCountAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListChannelBadgeCountRequest { ClanId = clanId };
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (page.HasValue)
            {
                request.Page = page.Value;
            }
            return SendSocketApiAsync("ListChannelBadgeCount", request, ListChannelBadgeCountResponse.Parser, options);
        }

        public override Task<LogedDeviceList> ListLogedDeviceAsync(RequestOptions? options = null)
            => SendSocketApiAsync("ListLogedDevice", new NoParams(), LogedDeviceList.Parser, options);

        public override Task<ClanUserStatusList> ListClanUsersStatusAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanUsersStatusRequest { ClanId = clanId };
            return SendSocketApiAsync("ListClanUsersStatus", request, ClanUserStatusList.Parser, options);
        }

        public override Task<ListChannelTimelineResponse> ListChannelTimelineAsync(ListChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("ListChannelTimeline", body, ListChannelTimelineResponse.Parser, options);
        }

        public override Task<ListArchivedChannelDescsResponse> ListArchivedChannelDescsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListArchivedChannelDescsRequest { ClanId = clanId };
            return SendSocketApiAsync("ListArchivedChannelDescs", request, ListArchivedChannelDescsResponse.Parser, options);
        }

        public override Task<ListUserOnlineResponse> ListUserOnlineAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListUserOnlineRequest { ClanId = clanId };
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }
            if (page.HasValue)
            {
                request.Page = page.Value;
            }
            return SendSocketApiAsync("ListUserOnline", request, ListUserOnlineResponse.Parser, options);
        }

        public override Task<MezonSession> RegistrationEmailAsync(global::Mezon.Net.Internal.Api.RegistrationEmailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("RegistrationEmail", body, MezonSession.Parser, options);
        }

        public override Task<UploadAttachment> UploadAttachmentFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UploadAttachmentFile", body, UploadAttachment.Parser, options);
        }

        public override Task<UploadAttachment> UploadOauthFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UploadOauthFile", body, UploadAttachment.Parser, options);
        }

        public override Task<Role> CreateRoleAsync(global::Mezon.Net.Internal.Api.CreateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateRole", body, Role.Parser, options);
        }

        public override Task<EventManagement> CreateEventAsync(global::Mezon.Net.Internal.Api.CreateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateEvent", body, EventManagement.Parser, options);
        }

        public override async Task ArchiveChannelAsync(ArchiveChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ArchiveChannel", body, Empty.Parser, options);
        }

        public override Task<LinkInviteUser> CreateLinkInviteUserAsync(global::Mezon.Net.Internal.Api.LinkInviteUserRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateLinkInviteUser", body, LinkInviteUser.Parser, options);
        }

        public override async Task SetNotificationClanSettingAsync(global::Mezon.Net.Internal.Api.SetDefaultNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SetNotificationClanSetting", body, Empty.Parser, options);
        }

        public override async Task UpdateAccountAsync(Internal.Api.UpdateAccountRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateAccount", body, Empty.Parser, options);
        }

        public override Task<MezonSession> UpdateUsernameAsync(UpdateUsernameRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UpdateUsername", body, MezonSession.Parser, options);
        }

        public override async Task UpdateCategoryOrderAsync(global::Mezon.Net.Internal.Api.UpdateCategoryOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateCategoryOrder", body, Empty.Parser, options);
        }

        public override async Task UpdateRoleAsync(global::Mezon.Net.Internal.Api.UpdateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateRole", body, Empty.Parser, options);
        }

        public override async Task UpdateEventAsync(global::Mezon.Net.Internal.Api.UpdateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateEvent", body, Empty.Parser, options);
        }

        public override Task<global::Mezon.Net.Internal.Api.SearchMessageResponse> SearchMessageAsync(global::Mezon.Net.Internal.Api.SearchMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("SearchMessage", body, global::Mezon.Net.Internal.Api.SearchMessageResponse.Parser, options);
        }

        public override async Task HandleWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("HandleWebhook", body, Empty.Parser, options);
        }

        public override Task<CheckDuplicateNameResponse> CheckDuplicateNameAsync(CheckDuplicateNameRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CheckDuplicateName", body, CheckDuplicateNameResponse.Parser, options);
        }

        public override Task<App> AddAppAsync(global::Mezon.Net.Internal.Api.AddAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("AddApp", body, App.Parser, options);
        }

        public override Task<UserActivity> CreateActivityAsync(global::Mezon.Net.Internal.Api.CreateActivityRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateActiviy", body, UserActivity.Parser, options);
        }

        public override async Task UpdateUserCustomStatusAsync(User body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("UpdateUserCustomStatus", body, Empty.Parser, options);
        }

        public override Task<global::Mezon.Net.Internal.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null)
            => SendSocketApiAsync("CreateExternalMezonMeet", new Empty(), global::Mezon.Net.Internal.Api.GenerateMezonMeetResponse.Parser, options);

        public override Task<UpdateChannelTimelineResponse> UpdateChannelTimelineAsync(UpdateChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UpdateChannelTimeline", body, UpdateChannelTimelineResponse.Parser, options);
        }

        public override Task<CreateChannelTimelineResponse> CreateChannelTimelineAsync(CreateChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreateChannelTimeline", body, CreateChannelTimelineResponse.Parser, options);
        }

        public override Task<ChannelTimelineDetailResponse> DetailChannelTimelineAsync(ChannelTimelineDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("DetailChannelTimeline", body, ChannelTimelineDetailResponse.Parser, options);
        }

        public override Task<CreatePollResponse> CreatePollAsync(CreatePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("CreatePoll", body, CreatePollResponse.Parser, options);
        }

        public override Task<VotePollResponse> VotePollAsync(VotePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("VotePoll", body, VotePollResponse.Parser, options);
        }

        public override async Task ClosePollAsync(ClosePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ClosePoll", body, Empty.Parser, options);
        }

        public override Task<GetPollResponse> GetPollAsync(GetPollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("GetPoll", body, GetPollResponse.Parser, options);
        }

        public override async Task ReactChannelMessageAsync(MessageReaction body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("ReactChannelMessage", body, Empty.Parser, options);
        }

        public override Task<MultipartUploadAttachment> MultipartUploadAttachmentFileStartAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("MultipartUploadAttachmentFileStart", body, MultipartUploadAttachment.Parser, options);
        }

        public override Task<UploadAttachment> MultipartUploadAttachmentFileFinishAsync(MultipartUploadAttachmentFinishRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("MultipartUploadAttachmentFileFinish", body, UploadAttachment.Parser, options);
        }

        public override async Task SessionLogoutAsync(SessionLogoutRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendSocketApiAsync("SessionLogout", body, Empty.Parser, options);
        }

        public override Task<UploadAttachmentBatch> UploadBatchAttachmentFileAsync(UploadBatchAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendSocketApiAsync("UploadBatchAttachmentFile", body, UploadAttachmentBatch.Parser, options);
        }

        #endregion
    }
}
