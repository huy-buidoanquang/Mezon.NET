using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Internal.Realtime;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Client
{
    internal partial class MezonSocketApiClient : MezonApiClient, IMezonSocketClient, IDisposable, IAsyncDisposable
    {
        private int _nextCid = 1;
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
            WebSocketClient = networkTransportProvider(TransportType.Auto);
            WebSocketClient.Opened += WebSocketClient_Opened;
            WebSocketClient.Closed += WebSocketClient_Closed;
            WebSocketClient.ErrorOccurred += WebSocketClient_ErrorOccurred;
            WebSocketClient.MessageReceived += WebSocketClient_MessageReceived;
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
                throw new InvalidOperationException("The client must be logged in before connecting.");
            }

            if (WebSocketClient == null)
            {
                throw new NotSupportedException("This client is not configured with WebSocket support.");
            }

            //RequestQueue.ClearGatewayBuckets();

            ConnectionState = ConnectionState.Connecting;
            try
            {
                _connectCancelToken?.Dispose();
                _connectCancelToken = new CancellationTokenSource();
                WebSocketClient.SetCancelToken(_connectCancelToken.Token);
                var (host, port, token) = GetWebSocketUrl();
                await WebSocketClient.ConnectAsync(host, port, token, true).ConfigureAwait(false);
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

            await WebSocketClient.SendAsync(type, GenerateCid(), bytes).ConfigureAwait(false);
            //await RequestQueue.SendAsync(new WebSocketRequest(WebSocketClient, bytes, false, options)).ConfigureAwait(false);
            await _socketSentMessageEvent.InvokeAsync($"Sent: {type} {bytes.Length} bytes").ConfigureAwait(false);

        }

        public Task SendAsync(MezonMessageType type, Envelope envelope, RequestOptions? options = null)
            => SendInternalAsync(type, envelope, options);

        private async Task SendInternalAsync(MezonMessageType type, Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            envelope.Cid = GenerateCid();
            await WebSocketClient.SendAsync(type, envelope.Cid, envelope.ToByteArray()).ConfigureAwait(false);
            //await RequestQueue.SendAsync(new WebSocketRequest(WebSocketClient, bytes, false, options)).ConfigureAwait(false);
            await _socketSentMessageEvent.InvokeAsync($"Sent: {type} {envelope.ToString()} bytes").ConfigureAwait(false);

        }

        public async Task Heartbeat(RequestOptions? options = null)
        {
            _lastPingSentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            options ??= RequestOptions.CreateOrClone(options);
            await SendAsync(MezonMessageType.Heartbeat, new byte[1], options);
        }

        public async Task JoinClanChat(long clanId, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);

            var envelope = new Envelope
            {
                ClanJoin = new ClanJoin { ClanId = clanId }
            };
            await SendAsync(MezonMessageType.Abridged, envelope, options);
        }

        public async Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);

            var envelope = new Envelope
            {
                ChannelJoin = new ChannelJoin { ClanId = clanId, ChannelId = channelId, ChannelType = channelType, IsPublic = isPublic }
            };
            await SendAsync(MezonMessageType.Abridged, envelope, options);
        }

        private (string host, int port, string token) GetWebSocketUrl()
        {
            var session = SessionManager<MezonApiClientOptions>.Instance.CurrentSession();
            string ws = session.WsUrl ?? "";
            var uri = ws.Split(':') ?? throw new InvalidOperationException("WebSocket URL is not available in the session.");
            return ("dev-mezon.nccsoft.vn", 7349, session.SessionId);
            //return (uri[0], Int32.Parse(uri[1]), session.AuthToken);
            //return $"{scheme}{wsUrl}{port}/ws?lang=en&token={Uri.EscapeDataString(AuthToken)}&format=protobuf";
        }

        private int GenerateCid()
        {
            var cid = _nextCid;
            if (cid >= ushort.MaxValue)
            {
                _nextCid = 1;
            }
            else
            {
                Interlocked.Increment(ref _nextCid);
            }
            return cid;
        }
        #region Event Handlers
        private Task WebSocketClient_Opened()
        {
            return Task.CompletedTask;
        }

        private async Task WebSocketClient_ErrorOccurred(Exception exception)
        {
            Console.WriteLine($"WebSocket error occurred: {exception.Message}");
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
            if (data.Length == 0 && type != MezonMessageType.Heartbeat)
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

                if (_receivedMessageEvent.HasSubscribers && (cid == 0 || !WasPendingRequest(cid)))
                {
                    _ = _receivedMessageEvent.InvokeAsync(type, cid, code, type == MezonMessageType.Api ? data : null, envelope);
                }
            }
            catch (Exception)
            {
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
    }
}
