using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Queue;
using Mezon.Net.Transport;
using Mezon.Net.Utils;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;
using AddAppRequest = Mezon.Net.Internal.Api.AddAppRequest;
using CreateActivityRequest = Mezon.Net.Internal.Api.CreateActivityRequest;
using CreateEventRequest = Mezon.Net.Internal.Api.CreateEventRequest;
using CreateRoleRequest = Mezon.Net.Internal.Api.CreateRoleRequest;
using LinkInviteUserRequest = Mezon.Net.Internal.Api.LinkInviteUserRequest;
using PbSession = Mezon.Net.Internal.Api.Session;
using RegistrationEmailRequest = Mezon.Net.Internal.Api.RegistrationEmailRequest;
using SearchMessageRequest = Mezon.Net.Internal.Api.SearchMessageRequest;
using SearchMessageResponse = Mezon.Net.Internal.Api.SearchMessageResponse;
using SetDefaultNotificationRequest = Mezon.Net.Internal.Api.SetDefaultNotificationRequest;
using UpdateCategoryOrderRequest = Mezon.Net.Internal.Api.UpdateCategoryOrderRequest;
using UpdateEventRequest = Mezon.Net.Internal.Api.UpdateEventRequest;
using UpdateRoleRequest = Mezon.Net.Internal.Api.UpdateRoleRequest;
using UploadAttachmentRequest = Mezon.Net.Internal.Api.UploadAttachmentRequest;
using GenerateMezonMeetResponse = Mezon.Net.Internal.Api.GenerateMezonMeetResponse;

namespace Mezon.Net.Client
{
    internal class MezonSocketApiClient : MezonApiClient, IMezonSocketClient, IDisposable, IAsyncDisposable
    {
        private Action<string>? _wireTrace;
        private Action<string>? _wireWarning;

        private readonly TransportType _transportType;
        private readonly SocketRequestHub _requestHub = new();
        private long _lastPingSentMs;
        public event Func<string, Task> SocketSentMessageEvent { add { _socketSentMessageEvent.Add(value); } remove { _socketSentMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, Task>> _socketSentMessageEvent = new AsyncEvent<Func<string, Task>>();

        public event Func<MezonMessageType, int, int, ReadOnlyMemory<byte>?, Envelope?, Task> ReceivedMessageEvent { add { _receivedMessageEvent.Add(value); } remove { _receivedMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<MezonMessageType, int, int, ReadOnlyMemory<byte>?, Envelope?, Task>> _receivedMessageEvent = new AsyncEvent<Func<MezonMessageType, int, int, ReadOnlyMemory<byte>?, Envelope?, Task>>();

        public event Func<Exception, Task> DisconnectedEvent { add { _disconnectedEvent.Add(value); } remove { _disconnectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();

        private CancellationTokenSource? _connectCancelToken;


        internal IMezonNetworkTransporter WebSocketClient { get; }

        public ConnectionState ConnectionState { get; private set; }

        public MezonSocketApiClient(RestClientProvider restClientProvider, MezonNetworkTransportProvider networkTransportProvider, MezonSocketClientOptions options)
            : base(restClientProvider, networkTransportProvider, options)
        {
            _transportType = options.TransportType.Resolve();
            WebSocketClient = networkTransportProvider(_transportType);
            WebSocketClient.Opened += WebSocketClient_Opened;
            WebSocketClient.Closed += WebSocketClient_Closed;
            WebSocketClient.ErrorOccurred += WebSocketClient_ErrorOccurred;
            WebSocketClient.MessageReceived += WebSocketClient_MessageReceived;
        }

        internal void ConfigureWireLogging(Action<string>? wireTrace, Action<string>? wireWarning)
        {
            _wireTrace = wireTrace;
            _wireWarning = wireWarning;
            WebSocketClient.WireTrace = wireTrace;
        }

        private void TraceWire(string message) => _wireTrace?.Invoke(message);

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
                throw new InvalidOperationException("The client must be logged in before connecting.");
            }

            if (WebSocketClient == null)
            {
                throw new NotSupportedException("This client is not configured with WebSocket support.");
            }

            RequestQueue.ClearGatewayBuckets();

            ConnectionState = ConnectionState.Connecting;
            try
            {
                _connectCancelToken?.Dispose();
                _connectCancelToken = new CancellationTokenSource();
                WebSocketClient.SetCancelToken(_connectCancelToken.Token);
                var (host, port, token) = GetTransportEndpoint();
                var socketOptions = (MezonSocketClientOptions)MezonOptions;
                await WebSocketClient.ConnectAsync(host, port, token, useSsl: true, createStatus: socketOptions.CreateStatusOnConnect).ConfigureAwait(false);
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
            if (WebSocketClient == null)
            {
                throw new NotSupportedException("This client is not configured with WebSocket support.");
            }

            if (ConnectionState == ConnectionState.Disconnected)
            {
                return;
            }

            ConnectionState = ConnectionState.Disconnecting;
            await WebSocketClient.DisconnectAsync().ConfigureAwait(false);
            try
            {
                _connectCancelToken?.Cancel(false);
            }
            catch { }

            ConnectionState = ConnectionState.Disconnected;
        }

        public Task SendAsync(MezonMessageType type, ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
            => SendInternalAsync(type, bytes, options);

        private async Task SendInternalAsync(MezonMessageType type, ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            await SendSocketPayloadAsync(type, cid, bytes.ToArray(), options).ConfigureAwait(false);
            await _socketSentMessageEvent.InvokeAsync($"Sent: {type} {bytes.Length} bytes").ConfigureAwait(false);
        }

        public Task SendAsync(MezonMessageType type, Envelope envelope, RequestOptions? options = null)
            => SendInternalAsync(type, envelope, options);

        private async Task SendInternalAsync(MezonMessageType type, Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            envelope.Cid = _requestHub.AllocateCid();
            var bucketType = envelope.Status != null ? SocketBucketType.PresenceUpdate : SocketBucketType.Unbucketed;
            await SendSocketPayloadAsync(type, envelope.Cid, envelope.ToByteArray(), options, bucketType).ConfigureAwait(false);
            await _socketSentMessageEvent.InvokeAsync($"Sent: {type} {envelope}").ConfigureAwait(false);
        }

        public async Task Heartbeat(RequestOptions? options = null)
        {
            _lastPingSentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            var timeout = options.SocketSendTimeout ?? SocketRequestHub.DefaultTimeoutMilliseconds;
            var waitTask = _requestHub.WaitAsync(cid, timeout, options.CancelToken);
            if (_wireTrace != null)
            {
                TraceWire($"[WIRE-OUT] heartbeat cid={cid} timeout={timeout}ms");
            }

            try
            {
                if (_transportType == TransportType.WebSocket)
                {
                    var envelope = new Envelope { Cid = cid, Ping = new Ping() };
                    await SendSocketPayloadAsync(MezonMessageType.Abridged, cid, envelope.ToByteArray(), options, bypassGatewayLimiter: true).ConfigureAwait(false);
                }
                else
                {
                    await SendSocketPayloadAsync(MezonMessageType.Heartbeat, cid, Array.Empty<byte>(), options, bypassGatewayLimiter: true).ConfigureAwait(false);
                }

                await waitTask.ConfigureAwait(false);
            }
            catch
            {
                WebSocketClient.ResetApiStream(cid);
                throw;
            }
        }

        public Task JoinClanChat(long clanId, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            var envelope = new Envelope
            {
                ClanJoin = new ClanJoin { ClanId = clanId }
            };
            return SendEnvelopeAsync(envelope, options);
        }

        public Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            var envelope = new Envelope
            {
                ChannelJoin = new ChannelJoin { ClanId = clanId, ChannelId = channelId, ChannelType = channelType, IsPublic = isPublic }
            };
            return SendEnvelopeAsync(envelope, options);
        }

        private (string host, int port, string token) GetTransportEndpoint()
        {
            var session = SessionManager<MezonApiClientOptions>.Instance.CurrentSession();
            var connectToken = !string.IsNullOrEmpty(session.SessionId)
                ? session.SessionId
                : session.AuthToken ?? string.Empty;
            var endpointUrl = _transportType == TransportType.Tcp ? session.TcpUrl : session.WsUrl;
            if (string.IsNullOrWhiteSpace(endpointUrl))
            {
                return (MezonNetworkSettings.DefaultSocketHost, MezonNetworkSettings.DefaultSocketPort, connectToken);
            }

            var parts = endpointUrl.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var port))
            {
                return (parts[0], port, connectToken);
            }

            return (MezonNetworkSettings.DefaultSocketHost, MezonNetworkSettings.DefaultSocketPort, connectToken);
        }
        #region Event Handlers
        private Task WebSocketClient_Opened()
        {
            return Task.CompletedTask;
        }

        private async Task WebSocketClient_ErrorOccurred(Exception exception)
        {
            _wireWarning?.Invoke($"WebSocket error occurred: {exception.Message}");
        }

        private async Task WebSocketClient_Closed(Exception? exception)
        {
            await DisconnectAsync().ConfigureAwait(false);
            if (_disconnectedEvent.HasSubscribers)
            {
                await _disconnectedEvent.InvokeAsync(exception ?? new Exception("WebSocket closed.")).ConfigureAwait(false);
            }
        }

        private ValueTask WebSocketClient_MessageReceived(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0 && type is not MezonMessageType.Heartbeat and not MezonMessageType.Api)
            {
                return default;
            }

            try
            {
                Envelope? envelope = null;
                switch (type)
                {
                    case MezonMessageType.Abridged:
                        envelope = Envelope.Parser.ParseFrom(data.Span);
                        if (envelope.Pong != null)
                        {
                            type = MezonMessageType.Heartbeat;
                            cid = envelope.Cid;
                        }
                        else
                        {
                            cid = envelope.Cid;
                        }
                        break;
                }

                OnSocketMessageReceived(type, cid, code, data, envelope);

                if (_wireTrace != null)
                {
                    var envCase = envelope?.MessageCase.ToString() ?? "n/a";
                    TraceWire(
                        $"[WIRE-IN] type={type} cid={cid} code={code} bytes={data.Length} pending={_requestHub.PendingCount} env={envCase}");
                }

                // Heartbeat/pong completes pending ping requests in OnSocketMessageReceived; do not fan out to event handlers.
                if (type != MezonMessageType.Heartbeat
                    && _receivedMessageEvent.HasSubscribers
                    && (cid == 0 || !WasPendingRequest(cid)))
                {
                    _ = _receivedMessageEvent.InvokeAsync(type, cid, code, type == MezonMessageType.Api ? data : null, envelope);
                }
            }
            catch (Exception ex)
            {
                if (_wireTrace != null)
                {
                    TraceWire($"[WIRE-IN] parse error type={type} cid={cid}: {ex.Message}");
                }
            }

#if NET6_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask();
#endif
        }

        private bool WasPendingRequest(int cid) => cid > 0 && _requestHub.Contains(cid);
        #endregion

        internal override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _connectCancelToken?.Dispose();
                    (WebSocketClient as IDisposable)?.Dispose();
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
                    (WebSocketClient as IDisposable)?.Dispose();
                }
            }

            return base.DisposeAsync(disposing);
        }

        #region Socket API

        public int LatencyMilliseconds { get; private set; }

        internal int PendingSocketRequestCount => _requestHub.PendingCount;

        public Task<TResponse> SendApiAsync<TRequest, TResponse>(
            string apiName,
            TRequest request,
            MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TRequest : IMessage<TRequest>
            where TResponse : IMessage<TResponse>
        {
            if (!ApiNameIndexMap.TryGetIndex(apiName, out var apiIndex))
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

            if (_wireTrace != null)
            {
                TraceWire(
                    $"[WIRE-OUT] api={apiName} index={apiIndex} body_bytes={request.CalculateSize()}");
            }

            return SendApiEnvelopeAsync(envelope, responseParser, options);
        }

        public async Task<TResponse> SendApiEnvelopeAsync<TResponse>(
            Envelope envelope,
            MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TResponse : IMessage<TResponse>
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? SocketRequestHub.DefaultTimeoutMilliseconds;
            var waitTask = _requestHub.WaitAsync(cid, timeout, options.CancelToken);
            var payload = envelope.ToByteArray();
            if (_wireTrace != null)
            {
                TraceWire($"[WIRE-OUT] abridged cid={cid} bytes={payload.Length} timeout={timeout}ms");
            }

            try
            {
                await SendSocketPayloadAsync(MezonMessageType.Abridged, cid, payload, options, SocketBucketType.Unbucketed).ConfigureAwait(false);
                var socketResponse = await waitTask.ConfigureAwait(false);

                if (socketResponse.Code != 0)
                {
                    throw new RPCException(new Grpc.Core.Status((StatusCode)socketResponse.Code, $"Socket API failed with code {socketResponse.Code}"));
                }

                return responseParser.ParseFrom(socketResponse.Payload.Span);
            }
            catch
            {
                WebSocketClient.ResetApiStream(cid);
                throw;
            }
        }

        public async Task<Envelope> SendEnvelopeAsync(Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? SocketRequestHub.DefaultTimeoutMilliseconds;
            var waitTask = _requestHub.WaitAsync(cid, timeout, options.CancelToken);
            var payload = envelope.ToByteArray();
            try
            {
                await SendSocketPayloadAsync(MezonMessageType.Abridged, cid, payload, options, SocketBucketType.Unbucketed).ConfigureAwait(false);
                var socketResponse = await waitTask.ConfigureAwait(false);

                if (socketResponse.Code != 0)
                {
                    throw new RPCException(new Grpc.Core.Status((StatusCode)socketResponse.Code, $"Socket envelope failed with code {socketResponse.Code}"));
                }

                if (socketResponse.Payload.Length > 0)
                {
                    return Envelope.Parser.ParseFrom(socketResponse.Payload.Span);
                }

                return envelope;
            }
            catch
            {
                WebSocketClient.ResetApiStream(cid);
                throw;
            }
        }

        internal async Task SendSocketPayloadAsync(
            MezonMessageType type,
            int cid,
            byte[] payload,
            RequestOptions options,
            SocketBucketType bucketType = SocketBucketType.Unbucketed,
            bool bypassGatewayLimiter = false)
        {
            options.BucketId ??= SocketBucket.Get(bucketType).Id;
            if (!bypassGatewayLimiter)
            {
                await RequestQueue.EnterGatewayAsync(options, bucketType).ConfigureAwait(false);
            }

            await WebSocketClient.SendAsync(type, cid, payload).ConfigureAwait(false);
        }

        private void OnSocketMessageReceived(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data, Envelope? envelope)
        {
            if (type == MezonMessageType.Heartbeat)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (_lastPingSentMs > 0)
                {
                    LatencyMilliseconds = (int)Math.Max(0, now - _lastPingSentMs);
                }

                var matched = _requestHub.TryComplete(cid, code, ReadOnlyMemory<byte>.Empty);
                if (_wireTrace != null)
                {
                    TraceWire($"[WIRE-COMPLETE] heartbeat cid={cid} matched={matched}");
                }

                return;
            }

            if (type == MezonMessageType.Api)
            {
                var matched = _requestHub.TryComplete(cid, code, data);
                if (_wireTrace != null)
                {
                    TraceWire(
                        $"[WIRE-COMPLETE] api cid={cid} code={code} bytes={data.Length} matched={matched}");
                }

                return;
            }

            if (envelope != null && envelope.Cid > 0)
            {
                var matched = _requestHub.TryComplete(envelope.Cid, code, envelope.ToByteArray());
                if (_wireTrace != null)
                {
                    TraceWire(
                        $"[WIRE-COMPLETE] abridged cid={envelope.Cid} env={envelope.MessageCase} matched={matched}");
                }
            }
        }

        public override Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null)
        {
            var request = new ListClanDescRequest
            {
                Limit = args.Limit.GetValueOrDefault(50),
                State = args.State.GetValueOrDefault(0),
                Cursor = args.Cursor.GetValueOrDefault(string.Empty),
            };
            return SendApiAsync("ListClanDescs", request, ClanDescList.Parser, options);
        }

        public override async Task<AuthenticationResponse> RefreshSessionAsync(
            string basicAuthUsername,
            string basicAuthPassword,
            Mezon.Net.Api.SessionRefreshRequest body,
            RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            var request = new Internal.Api.SessionRefreshRequest
            {
                IsRemember = body.IsRemember ?? false,
                Token = body.Token,
            };
            if (body.Vars != null)
            {
                foreach (var pair in body.Vars)
                {
                    request.Vars[pair.Key] = pair.Value;
                }
            }

            var session = await SendApiAsync("SessionRefresh", request, global::Mezon.Net.Internal.Api.Session.Parser, options).ConfigureAwait(false);
            return new AuthenticationResponse
            {
                ApiUrl = session.ApiUrl,
                WsUrl = session.WsUrl,
                TcpUrl = session.TcpUrl,
                Created = session.Created,
                IsRemember = session.IsRemember,
                RefreshToken = session.RefreshToken,
                Token = session.Token,
                UserId = session.UserId,
            };
        }

public override async Task DeleteAccountAsync(RequestOptions? options = null)
        {
            await SendApiAsync("DeleteAccount", new Empty(), Empty.Parser, options);
        }

        public override Task<Account> GetAccountAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetAccount", new Empty(), Account.Parser, options);
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
            return SendApiAsync("AddFriends", request, AddFriendsResponse.Parser, options);
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
            await SendApiAsync("BlockFriends", request, Empty.Parser, options);
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
            await SendApiAsync("UnblockFriends", request, Empty.Parser, options);
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
            await SendApiAsync("DeleteFriends", request, Empty.Parser, options);
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
            return SendApiAsync("ListFriends", request, FriendList.Parser, options);
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
            return SendApiAsync("CreateClanDesc", request, ClanDesc.Parser, options);
        }

        public override async Task DeleteClanDescAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteClanDescRequest();
            request.ClanDescId = clanId;
            await SendApiAsync("DeleteClanDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanDesc", body, Empty.Parser, options);
        }

        public override Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanUsersRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListClanUsers", request, ClanUserList.Parser, options);
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
            await SendApiAsync("RemoveClanUsers", request, Empty.Parser, options);
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
            await SendApiAsync("BanClanUsers", request, Empty.Parser, options);
        }

        public override Task<Internal.Api.ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateChannelDesc", body, Internal.Api.ChannelDescription.Parser, options);
        }

        public override async Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("DeleteChannelDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelDesc", body, Empty.Parser, options);
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
            await SendApiAsync("AddChannelUsers", request, Empty.Parser, options);
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
            await SendApiAsync("RemoveChannelUsers", request, Empty.Parser, options);
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
            return SendApiAsync("ListChannelMessages", request, ChannelMessageList.Parser, options);
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
            return SendApiAsync("ListChannelUsers", request, ChannelUserList.Parser, options);
        }

        public override async Task DeleteRoleAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendApiAsync("DeleteRole", request, Empty.Parser, options);
        }

        public override Task<RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
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
            return SendApiAsync("ListRoles", request, RoleListEventResponse.Parser, options);
        }

        public override async Task UpdateUserAsync(UpdateUsersRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUser", body, Empty.Parser, options);
        }

        public override async Task DeleteEventAsync(long eventId, RequestOptions? options = null)
        {
            var request = new DeleteEventRequest();
            request.EventId = eventId;
            await SendApiAsync("DeleteEvent", request, Empty.Parser, options);
        }

        public override Task<EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListEventsRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            return SendApiAsync("ListEvents", request, EventList.Parser, options);
        }

        public override Task<ChannelMessage> CreatePinMessageAsync(PinMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreatePinMessage", body, ChannelMessage.Parser, options);
        }

        public override Task<PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new PinMessageRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendApiAsync("GetPinMessagesList", request, PinMessagesList.Parser, options);
        }

        public override async Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new DeletePinMessage();
            request.MessageId = messageId;
            request.ChannelId = channelId;
            request.ClanId = clanId;
            await SendApiAsync("DeletePinMessage", request, Empty.Parser, options);
        }

        public override async Task MarkAsReadAsync(MarkAsReadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MarkAsRead", body, Empty.Parser, options);
        }

        public override async Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("CreateClanEmoji", body, Empty.Parser, options);
        }

        public override async Task UpdateClanEmojiByIdAsync(ClanEmojiUpdateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanEmojiById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanEmojiDeleteRequest();
            request.Id = emojiId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteByIdClanEmoji", request, Empty.Parser, options);
        }

        public override async Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddClanSticker", body, Empty.Parser, options);
        }

        public override async Task UpdateClanStickerByIdAsync(ClanStickerUpdateByIdRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanStickerById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanStickerDeleteRequest();
            request.Id = stickerId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteClanStickerById", request, Empty.Parser, options);
        }

        public override Task<EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListEmojisByUserId", new Empty(), EmojiListedResponse.Parser, options);
        }

        public override Task<StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListStickersByUserId", new Empty(), StickerListedResponse.Parser, options);
        }

        public override Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateWebhook", body, WebhookGenerateResponse.Parser, options);
        }

        public override Task<WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new WebhookListRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendApiAsync("ListWebhookByChannelId", request, WebhookListResponse.Parser, options);
        }

        public override async Task UpdateWebhookByIdAsync(WebhookUpdateRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateWebhookById", body, Empty.Parser, options);
        }

        public override async Task DeleteWebhookByIdAsync(WebhookDeleteRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteWebhookById", body, Empty.Parser, options);
        }

        public override async Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("CreateSystemMessage", body, Empty.Parser, options);
        }

        public override async Task UpdateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateSystemMessage", body, Empty.Parser, options);
        }

        public override Task<SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null)
        {
            var request = new GetSystemMessage();
            request.ClanId = clanId;
            return SendApiAsync("GetSystemMessageByClanId", request, SystemMessage.Parser, options);
        }

        public override async Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteSystemMessage();
            request.ClanId = clanId;
            await SendApiAsync("DeleteSystemMessage", request, Empty.Parser, options);
        }

        public override async Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateRoleOrder", body, Empty.Parser, options);
        }

        public override async Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanOrder", body, Empty.Parser, options);
        }

        public override Task<ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChanEncryptionMethod();
            request.ChannelId = channelId;
            return SendApiAsync("GetChanEncryptionMethod", request, ChanEncryptionMethod.Parser, options);
        }

        public override async Task SetChanEncryptionMethodAsync(ChanEncryptionMethod body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetChanEncryptionMethod", body, Empty.Parser, options);
        }

        public override Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new GetPubKeysRequest();
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            return SendApiAsync("GetPubKeys", request, GetPubKeysResponse.Parser, options);
        }

        public override async Task PushPublicKeyAsync(PushPubKeyRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("PushPubKey", body, Empty.Parser, options);
        }

        public override Task<GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetKeyServer", new Empty(), GetKeyServerResp.Parser, options);
        }

        public override Task<ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null)
        {
            var request = new ListOnboardingRequest();
            request.ClanId = clanId;
            if (guideType.HasValue)
            {
            request.GuideType = guideType.Value;
            }
            return SendApiAsync("ListOnboarding", request, ListOnboardingResponse.Parser, options);
        }

        public override Task<OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            return SendApiAsync("GetOnboardingDetail", request, OnboardingItem.Parser, options);
        }

        public override Task<ListOnboardingResponse> CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateOnboarding", body, ListOnboardingResponse.Parser, options);
        }

        public override async Task UpdateOnboardingAsync(UpdateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateOnboarding", body, Empty.Parser, options);
        }

        public override async Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            await SendApiAsync("DeleteOnboarding", request, Empty.Parser, options);
        }

        public override Task<ListUserActivity> ListActivityAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListActivity", new Empty(), ListUserActivity.Parser, options);
        }

        public override Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateMeetToken", body, GenerateMeetTokenResponse.Parser, options);
        }

        public override async Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("TransferOwnership", body, Empty.Parser, options);
        }

        public override Task<PermissionList> GetListPermissionAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListPermission", new Empty(), PermissionList.Parser, options);
        }

        public override Task<PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null)
        {
            var request = new ListPermissionsRequest();
            request.RoleId = roleId;
            return SendApiAsync("ListRolePermissions", request, PermissionList.Parser, options);
        }

        public override Task<RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null)
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
            return SendApiAsync("ListRoleUsers", request, RoleUserList.Parser, options);
        }

        public override Task<UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null)
        {
            var request = new UserPermissionInChannelListRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            return SendApiAsync("ListUserPermissionInChannel", request, UserPermissionInChannelListResponse.Parser, options);
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
            await SendApiAsync("DeleteNotifications", request, Empty.Parser, options);
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
            return SendApiAsync("ListNotifications", request, NotificationList.Parser, options);
        }

        public override Task<CategoryDesc> CreateCategoryDescAsync(CreateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateCategoryDesc", body, CategoryDesc.Parser, options);
        }

        public override async Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null)
        {
            var request = new DeleteCategoryDescRequest();
            request.CategoryId = categoryId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteCategoryDesc", request, Empty.Parser, options);
        }

        public override async Task UpdateCategoryAsync(UpdateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateCategory", body, Empty.Parser, options);
        }

        public override Task<CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new CategoryDesc();
            request.ClanId = clanId;
            return SendApiAsync("ListCategoryDescs", request, CategoryDescList.Parser, options);
        }

        public override Task<InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null)
        {
            var request = new InviteUserRequest();
            request.InviteId = inviteId;
            return SendApiAsync("InviteUser", request, InviteUserRes.Parser, options);
        }

        public override async Task SetNotificationChannelSettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetNotificationChannelSetting", body, Empty.Parser, options);
        }

        public override async Task SetMuteNotificationCategoryAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetMuteCategory", body, Empty.Parser, options);
        }

        public override async Task SetMuteNotificationChannelAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetMuteChannel", body, Empty.Parser, options);
        }

        public override Task<NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendApiAsync("GetChannelCategoryNotiSettingsList", request, NotificationChannelCategorySettingList.Parser, options);
        }

        public override Task<NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendApiAsync("GetNotificationClan", request, NotificationSetting.Parser, options);
        }

        public override Task<UserStatus> GetUserStatusAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetUserStatus", new Empty(), UserStatus.Parser, options);
        }

        public override async Task UpdateUserStatusAsync(UserStatusUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUserStatus", body, Empty.Parser, options);
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
            return SendApiAsync("ListApps", request, AppList.Parser, options);
        }

        public override Task<App> GetAppAsync(long id, RequestOptions? options = null)
        {
            var request = new AppId();
            request.Id = id;
            return SendApiAsync("GetApp", request, App.Parser, options);
        }

        public override Task<App> UpdateAppAsync(UpdateAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateApp", body, App.Parser, options);
        }

        public override async Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null)
        {
            var request = new AppDeleteRequest();
            request.Id = id;
            if (recordDeletion.HasValue)
            {
            request.RecordDeletion = recordDeletion.Value;
            }
            await SendApiAsync("DeleteApp", request, Empty.Parser, options);
        }

        public override async Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null)
        {
            var request = new AppClan();
            request.AppId = appId;
            request.ClanId = clanId;
            await SendApiAsync("AddAppToClan", request, Empty.Parser, options);
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
            return SendApiAsync("ListAuditLog", request, ListAuditLog.Parser, options);
        }

        public override async Task AddUserEventAsync(UserEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddUserEvent", body, Empty.Parser, options);
        }

        public override async Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null)
        {
            var request = new UserEventRequest();
            request.ClanId = clanId;
            request.EventId = eventId;
            await SendApiAsync("DeleteUserEvent", request, Empty.Parser, options);
        }

        public override async Task HealthcheckAsync(RequestOptions? options = null)
        {
            await SendApiAsync("Healthcheck", new Empty(), Empty.Parser, options);
        }

        public override Task<ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, int? channelType = null, bool? isMobile = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListChannelDescsRequest();
            request.ClanId = clanId;
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
            return SendApiAsync("ListChannelDescs", request, ChannelDescList.Parser, options);
        }

        public override Task<Internal.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelDetailRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListChannelDetail", request, Internal.Api.ChannelDescription.Parser, options);
        }

        public override Task<BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new BannedUserListRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListBannedUsers", request, BannedUserList.Parser, options);
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
            await SendApiAsync("UnbanClanUsers", request, Empty.Parser, options);
        }

        public override Task<RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(RegistFcmDeviceTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("RegistFCMDeviceToken", body, RegistFcmDeviceTokenResponse.Parser, options);
        }

        public override Task<AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListUserClansByUserId", new Empty(), AllUserClans.Parser, options);
        }

        public override Task<ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListChannelAppsRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            return SendApiAsync("ListChannelApps", request, ListChannelAppsResponse.Parser, options);
        }

        public override async Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("CloseDMByChannelId", request, Empty.Parser, options);
        }

        public override async Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("OpenDMByChannelId", request, Empty.Parser, options);
        }

        public override Task<ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ClanProfileRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetUserProfileOnClan", request, ClanProfile.Parser, options);
        }

        public override async Task UpdateUserProfileByClanAsync(UpdateClanProfileRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUserProfileByClan", body, Empty.Parser, options);
        }

        public override async Task LeaveThreadAsync(long channelId, RequestOptions? options = null)
        {
            var request = new LeaveThreadRequest();
            request.ChannelId = channelId;
            await SendApiAsync("LeaveThread", request, Empty.Parser, options);
        }

        public override Task<ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListThreadRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListThreadDescs", request, ChannelDescListNoPool.Parser, options);
        }

        public override Task<ChannelDescList> SearchThreadAsync(SearchThreadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("SearchThread", body, ChannelDescList.Parser, options);
        }

        public override Task<LinkAccountConfirmRequest> LinkSMSAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("LinkSMS", body, LinkAccountConfirmRequest.Parser, options);
        }

        public override async Task ConfirmLinkMezonOTPAsync(LinkAccountConfirmRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ConfirmLinkMezonOTP", body, Empty.Parser, options);
        }

        public override Task<LinkAccountConfirmRequest> LinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("LinkEmail", body, LinkAccountConfirmRequest.Parser, options);
        }

        public override async Task UnlinkMezonAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UnlinkMezon", body, Empty.Parser, options);
        }

        public override async Task UnlinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UnlinkEmail", body, Empty.Parser, options);
        }

        public override Task<IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null)
        {
            var request = new IsBannedRequest();
            request.ChannelId = channelId;
            return SendApiAsync("IsBanned", request, IsBannedResponse.Parser, options);
        }

        public override async Task AddRolesChannelDescAsync(AddRoleChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddRolesChannelDesc", body, Empty.Parser, options);
        }

        public override async Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendApiAsync("DeleteRoleChannelDesc", request, Empty.Parser, options);
        }

        public override async Task SetRoleChannelPermissionAsync(UpdateRoleChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetRoleChannelPermission", body, Empty.Parser, options);
        }

        public override Task<RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListPermissionOfUsersRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetRoleOfUserInTheClan", request, RoleList.Parser, options);
        }

        public override Task<PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(PermissionRoleChannelListEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetPermissionByRoleIdChannelId", body, PermissionRoleChannelListEventResponse.Parser, options);
        }

        public override Task<ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelAttachmentRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListChannelAttachment", request, ChannelAttachmentList.Parser, options);
        }

        public override Task<VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendApiAsync("ListChannelVoiceUsers", request, VoiceChannelUserList.Parser, options);
        }

        public override Task<StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendApiAsync("ListStreamingChannelUsers", request, StreamingChannelUserList.Parser, options);
        }

        public override Task<ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListChannelByUserId", new Empty(), ChannelDescListNoPool.Parser, options);
        }

        public override Task<NotificationUserChannel> GetNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetNotificationChannel", body, NotificationUserChannel.Parser, options);
        }

        public override Task<NotificationUserChannel> GetNotificationCategoryAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetNotificationCategory", body, NotificationUserChannel.Parser, options);
        }

        public override async Task SetNotificationCategorySettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetNotificationCategorySetting", body, Empty.Parser, options);
        }

        public override async Task DeleteNotificationCategorySettingAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteNotificationCategorySetting", body, Empty.Parser, options);
        }

        public override async Task DeleteNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteNotificationChannel", body, Empty.Parser, options);
        }

        public override Task<ChannelMessage> CreateMessage2InboxAsync(Message2InboxRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateMessage2Inbox", body, ChannelMessage.Parser, options);
        }

        public override Task<ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ChannelSettingListRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListChannelSetting", request, ChannelSettingListResponse.Parser, options);
        }

        public override async Task UpdateChannelPrivateAsync(ChangeChannelPrivateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelPrivate", body, Empty.Parser, options);
        }

        public override async Task ChangeChannelCategoryAsync(ChangeChannelCategoryRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ChangeChannelCategory", body, Empty.Parser, options);
        }

        public override Task<EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null)
        {
            return SendApiAsync("EmojiRecentList", new Empty(), EmojiRecentList.Parser, options);
        }

        public override Task<AllUsersAddChannelResponse> ListChannelUsersUCAsync(AllUsersAddChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListChannelUsersUC", body, AllUsersAddChannelResponse.Parser, options);
        }

        public override Task<EditChannelCanvasResponse> EditChannelCanvasesAsync(EditChannelCanvasRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("EditChannelCanvases", body, EditChannelCanvasResponse.Parser, options);
        }

        public override Task<ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChannelCanvasListRequest();
            request.ChannelId = channelId;
            return SendApiAsync("GetChannelCanvasList", request, ChannelCanvasListResponse.Parser, options);
        }

        public override Task<ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null)
        {
            var request = new ChannelCanvasDetailRequest();
            request.Id = id;
            return SendApiAsync("GetChannelCanvasDetail", request, ChannelCanvasDetailResponse.Parser, options);
        }

        public override async Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null)
        {
            var request = new DeleteChannelCanvasRequest();
            request.CanvasId = canvasId;
            await SendApiAsync("DeleteChannelCanvas", request, Empty.Parser, options);
        }

        public override Task<ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListFavoriteChannelRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetListFavoriteChannel", request, ListFavoriteChannelResponse.Parser, options);
        }

        public override Task<AddFavoriteChannelResponse> AddChannelFavoriteAsync(AddFavoriteChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("AddChannelFavorite", body, AddFavoriteChannelResponse.Parser, options);
        }

        public override async Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null)
        {
            var request = new RemoveFavoriteChannelRequest();
            request.ChannelId = channelId;
            await SendApiAsync("RemoveChannelFavorite", request, Empty.Parser, options);
        }

        public override Task<GenerateClanWebhookResponse> GenerateClanWebhookAsync(GenerateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateClanWebhook", body, GenerateClanWebhookResponse.Parser, options);
        }

        public override Task<ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanWebhookRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListClanWebhook", request, ListClanWebhookResponse.Parser, options);
        }

        public override async Task UpdateClanWebhookByIdAsync(UpdateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanWebhookById", body, Empty.Parser, options);
        }

        public override async Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null)
        {
            var request = new ClanWebhookRequest();
            request.Id = id;
            await SendApiAsync("DeleteClanWebhookById", request, Empty.Parser, options);
        }

        public override Task<ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListOnboardingStepRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListOnboardingStep", request, ListOnboardingStepResponse.Parser, options);
        }

        public override async Task UpdateOnboardingStepAsync(UpdateOnboardingStepRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateOnboardingStep", body, Empty.Parser, options);
        }

        public override async Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteQuickMenuAccess", body, Empty.Parser, options);
        }

        public override async Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddQuickMenuAccess", body, Empty.Parser, options);
        }

        public override async Task UpdateQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateQuickMenuAccess", body, Empty.Parser, options);
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
            return SendApiAsync("ListQuickMenuAccess", request, QuickMenuAccessList.Parser, options);
        }

        public override Task<IsFollowerResponse> IsFollowerAsync(IsFollowerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("IsFollower", body, IsFollowerResponse.Parser, options);
        }

        public override Task<ChannelMessageAck> SendChannelMessageAsync(ChannelMessageSend body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("SendChannelMessage", body, ChannelMessageAck.Parser, options);
        }

        public override Task<ChannelMessageAck> SendChannelMessageAsync(in Mezon.Net.Api.SendChannelMessageParams message, RequestOptions? options = null)
        {
            var body = new ChannelMessageSend
            {
                ClanId = message.ClanId,
                ChannelId = message.ChannelId,
                Content = message.Content,
                IsPublic = message.IsPublic,
                Mode = message.Mode,
            };
            if (message.TopicId.HasValue)
            {
                body.TopicId = message.TopicId.Value;
            }

            return SendApiAsync("SendChannelMessage", body, ChannelMessageAck.Parser, options);
        }

        public override async Task UpdateChannelMessageAsync(ChannelMessageUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelMessage", body, Empty.Parser, options);
        }

        public override async Task DeleteChannelMessageAsync(ChannelMessageRemove body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteChannelMessage", body, Empty.Parser, options);
        }

        public override async Task RemoveParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("RemoveParticipantMezonMeet", body, Empty.Parser, options);
        }

        public override async Task MuteParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MuteParticipantMezonMeet", body, Empty.Parser, options);
        }

        public override Task<CreateRoomChannelApps> CreateRoomChannelAppsAsync(CreateRoomChannelApps body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateRoomChannelApps", body, CreateRoomChannelApps.Parser, options);
        }

        public override Task<GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(GenerateHashChannelAppsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateHashChannelApps", body, GenerateHashChannelAppsResponse.Parser, options);
        }

        public override Task<MezonOauthClient> GetMezonOauthClientAsync(GetMezonOauthClientRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public override async Task DeleteMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteMezonOauthClient", body, Empty.Parser, options);
        }

        public override Task<MezonOauthClient> UpdateMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public override Task<SdTopicList> ListSdTopicAsync(ListSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListSdTopic", body, SdTopicList.Parser, options);
        }

        public override Task<SdTopic> GetTopicDetailAsync(SdTopicDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetTopicDetail", body, SdTopic.Parser, options);
        }

        public override Task<SdTopic> CreateSdTopicAsync(SdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateSdTopic", body, SdTopic.Parser, options);
        }

        public override async Task DeleteSdTopicAsync(DeleteSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteSdTopic", body, Empty.Parser, options);
        }

        public override async Task MessageButtonClickAsync(MessageButtonClicked body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MessageButtonClick", body, Empty.Parser, options);
        }

        public override async Task DropdownBoxSelectedAsync(DropdownBoxSelected body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DropdownBoxSelected", body, Empty.Parser, options);
        }

        public override async Task ActiveArchivedThreadAsync(ActiveArchivedThread body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ActiveArchivedThread", body, Empty.Parser, options);
        }

        public override async Task AddAgentToChannelAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddAgentToChannel", body, Empty.Parser, options);
        }

        public override async Task DisconnectAgentAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DisconnectAgent", body, Empty.Parser, options);
        }

        public override async Task ReportMessageAbuseAsync(ReportMessageAbuseReqest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ReportMessageAbuse", body, Empty.Parser, options);
        }

        public override Task<StreamHttpCallbackResponse> StreamingServerCallbackAsync(StreamHttpCallbackRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("StreamingServerCallback", body, StreamHttpCallbackResponse.Parser, options);
        }

        public override Task<ForSaleItemList> ListForSaleItemsAsync(ListForSaleItemsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListForSaleItems", body, ForSaleItemList.Parser, options);
        }

        public override async Task HandleClanWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("HandleClanWebhook", body, Empty.Parser, options);
        }

public override Task<MutedChannelList> ListMutedChannelAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListMutedChannelRequest { ClanId = clanId };
            return SendApiAsync("ListMutedChannel", request, MutedChannelList.Parser, options);
        }

        public override Task<ListClanBadgeCountResponse> ListClanBadgeCountAsync(RequestOptions? options = null)
            => SendApiAsync("ListClanBadgeCount", new NoParams(), ListClanBadgeCountResponse.Parser, options);

        public override Task<ListChannelBadgeCountResponse> ListChannelBadgeCountAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListChannelBadgeCountRequest { ClanId = clanId };
            if (limit.HasValue) request.Limit = limit.Value;
            if (page.HasValue) request.Page = page.Value;
            return SendApiAsync("ListChannelBadgeCount", request, ListChannelBadgeCountResponse.Parser, options);
        }

        public override Task<LogedDeviceList> ListLogedDeviceAsync(RequestOptions? options = null)
            => SendApiAsync("ListLogedDevice", new NoParams(), LogedDeviceList.Parser, options);

        public override Task<ClanUserStatusList> ListClanUsersStatusAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanUsersStatusRequest { ClanId = clanId };
            return SendApiAsync("ListClanUsersStatus", request, ClanUserStatusList.Parser, options);
        }

        public override Task<ListChannelTimelineResponse> ListChannelTimelineAsync(ListChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListChannelTimeline", body, ListChannelTimelineResponse.Parser, options);
        }

        public override Task<ListArchivedChannelDescsResponse> ListArchivedChannelDescsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListArchivedChannelDescsRequest { ClanId = clanId };
            return SendApiAsync("ListArchivedChannelDescs", request, ListArchivedChannelDescsResponse.Parser, options);
        }

        public override Task<ListUserOnlineResponse> ListUserOnlineAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            var request = new ListUserOnlineRequest { ClanId = clanId };
            if (limit.HasValue) request.Limit = limit.Value;
            if (page.HasValue) request.Page = page.Value;
            return SendApiAsync("ListUserOnline", request, ListUserOnlineResponse.Parser, options);
        }

        public override Task<PbSession> RegistrationEmailAsync(RegistrationEmailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("RegistrationEmail", body, PbSession.Parser, options);
        }

        public override Task<UploadAttachment> UploadAttachmentFileAsync(UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UploadAttachmentFile", body, UploadAttachment.Parser, options);
        }

        public override Task<UploadAttachment> UploadOauthFileAsync(UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UploadOauthFile", body, UploadAttachment.Parser, options);
        }

        public override Task<Role> CreateRoleAsync(CreateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateRole", body, Role.Parser, options);
        }

        public override Task<EventManagement> CreateEventAsync(CreateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateEvent", body, EventManagement.Parser, options);
        }

        public override async Task ArchiveChannelAsync(ArchiveChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ArchiveChannel", body, Empty.Parser, options);
        }

        public override Task<LinkInviteUser> CreateLinkInviteUserAsync(LinkInviteUserRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateLinkInviteUser", body, LinkInviteUser.Parser, options);
        }

        public override async Task SetNotificationClanSettingAsync(SetDefaultNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetNotificationClanSetting", body, Empty.Parser, options);
        }

        public override async Task UpdateAccountAsync(Internal.Api.UpdateAccountRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateAccount", body, Empty.Parser, options);
        }

        public override Task<PbSession> UpdateUsernameAsync(UpdateUsernameRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateUsername", body, PbSession.Parser, options);
        }

        public override async Task UpdateCategoryOrderAsync(UpdateCategoryOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateCategoryOrder", body, Empty.Parser, options);
        }

        public override async Task UpdateRoleAsync(UpdateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateRole", body, Empty.Parser, options);
        }

        public override async Task UpdateEventAsync(UpdateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateEvent", body, Empty.Parser, options);
        }

        public override Task<SearchMessageResponse> SearchMessageAsync(SearchMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("SearchMessage", body, SearchMessageResponse.Parser, options);
        }

        public override async Task HandleWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("HandleWebhook", body, Empty.Parser, options);
        }

        public override Task<CheckDuplicateNameResponse> CheckDuplicateNameAsync(CheckDuplicateNameRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CheckDuplicateName", body, CheckDuplicateNameResponse.Parser, options);
        }

        public override Task<App> AddAppAsync(AddAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("AddApp", body, App.Parser, options);
        }

        public override Task<UserActivity> CreateActivityAsync(CreateActivityRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateActiviy", body, UserActivity.Parser, options);
        }

        public override async Task UpdateUserCustomStatusAsync(User body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUserCustomStatus", body, Empty.Parser, options);
        }

        public override Task<global::Mezon.Net.Internal.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null)
            => SendApiAsync("CreateExternalMezonMeet", new Empty(), GenerateMezonMeetResponse.Parser, options);

        public override Task<UpdateChannelTimelineResponse> UpdateChannelTimelineAsync(UpdateChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateChannelTimeline", body, UpdateChannelTimelineResponse.Parser, options);
        }

        public override Task<CreateChannelTimelineResponse> CreateChannelTimelineAsync(CreateChannelTimelineRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateChannelTimeline", body, CreateChannelTimelineResponse.Parser, options);
        }

        public override Task<ChannelTimelineDetailResponse> DetailChannelTimelineAsync(ChannelTimelineDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("DetailChannelTimeline", body, ChannelTimelineDetailResponse.Parser, options);
        }

        public override Task<CreatePollResponse> CreatePollAsync(CreatePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreatePoll", body, CreatePollResponse.Parser, options);
        }

        public override Task<VotePollResponse> VotePollAsync(VotePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("VotePoll", body, VotePollResponse.Parser, options);
        }

        public override async Task ClosePollAsync(ClosePollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ClosePoll", body, Empty.Parser, options);
        }

        public override Task<GetPollResponse> GetPollAsync(GetPollRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetPoll", body, GetPollResponse.Parser, options);
        }

        public override async Task ReactChannelMessageAsync(MessageReaction body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ReactChannelMessage", body, Empty.Parser, options);
        }

        public override Task<MultipartUploadAttachment> MultipartUploadAttachmentFileStartAsync(UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("MultipartUploadAttachmentFileStart", body, MultipartUploadAttachment.Parser, options);
        }

        public override Task<UploadAttachment> MultipartUploadAttachmentFileFinishAsync(MultipartUploadAttachmentFinishRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("MultipartUploadAttachmentFileFinish", body, UploadAttachment.Parser, options);
        }

        public override async Task SessionLogoutAsync(SessionLogoutRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SessionLogout", body, Empty.Parser, options);
        }

        public override Task<UploadAttachmentBatch> UploadBatchAttachmentFileAsync(UploadBatchAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UploadBatchAttachmentFile", body, UploadAttachmentBatch.Parser, options);
        }

        #endregion

    }
}
