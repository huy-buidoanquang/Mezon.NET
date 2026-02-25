using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mezon.NET.Abstractions;
using Mezon.NET.Api;
using Mezon.NET.Core;
using Mezon.NET.Queue;
using Mezon.Protobuf.Realtime;

namespace Mezon.NET.WebSocket
{
    internal class MezonSocketApiClient : MezonApiClient, IMezonSocketClient, IDisposable, IAsyncDisposable
    {
        public event Func<string, Task> SocketSentMessageEvent { add { _socketSentMessageEvent.Add(value); } remove { _socketSentMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, Task>> _socketSentMessageEvent = new AsyncEvent<Func<string, Task>>();

        public event Func<SocketMessageCode, Envelope, Task> ReceivedMessageEvent { add { _receivedMessageEvent.Add(value); } remove { _receivedMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<SocketMessageCode, Envelope, Task>> _receivedMessageEvent = new AsyncEvent<Func<SocketMessageCode, Envelope, Task>>();

        public event Func<Exception, Task> DisconnectedEvent { add { _disconnectedEvent.Add(value); } remove { _disconnectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();

        private CancellationTokenSource? _connectCancelToken;


        internal IWebSocketClient WebSocketClient { get; }

        public ConnectionState ConnectionState { get; private set; }

        public MezonSocketApiClient(RestClientProvider restClientProvider, GRPCClientProvider grpcClientProvider, WebSocketClientProvider webSocketClientProvider, MezonSocketClientConfiguration configuration)
            : base(restClientProvider, grpcClientProvider, configuration)
        {
            WebSocketClient = webSocketClientProvider();
            WebSocketClient.Opened += WebSocketClient_Opened;
            WebSocketClient.Ready += WebSocketClient_Ready;
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
                await WebSocketClient.ConnectAsync(GetWebSocketUrl()).ConfigureAwait(false);
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

        public Task SendAsync(string socketName, ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
            => SendInternalAsync(socketName, bytes, options);

        private async Task SendInternalAsync(string socketName, ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            await RequestQueue.SendAsync(new WebSocketRequest(WebSocketClient, bytes, false, options)).ConfigureAwait(false);
            await _socketSentMessageEvent.InvokeAsync($"Sent:     {socketName} {bytes.Length} bytes").ConfigureAwait(false);

        }

        public async Task Ping(RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            options.BucketType = BucketType.Unbucketed;
            var envelope = new Envelope
            {
                Ping = new Ping()
            };
            await SendAsync("Ping", envelope.ToByteArray(), options);
        }

        public async Task JoinClanChat(long clanId, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            options.BucketType = BucketType.Unbucketed;

            var envelope = new Envelope
            {
                ClanJoin = new ClanJoin { ClanId = clanId }
            };
            await SendAsync("JoinClanChat", envelope.ToByteArray(), options);
        }

        public async Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);

            var envelope = new Envelope
            {
                ChannelJoin = new ChannelJoin { ClanId = clanId, ChannelId = channelId, ChannelType = channelType, IsPublic = isPublic }
            };
            await SendAsync("JoinChannelChat", envelope.ToByteArray(), options);
        }

        private string GetWebSocketUrl()
        {
            var session = SessionManager.Instance.CurrentSession();
            var apiUri = new Uri(session.ApiUrl!);
            var scheme = apiUri.Scheme == Uri.UriSchemeHttps ? "wss://" : "ws://";
            var wsUrl = !string.IsNullOrEmpty(session.WsUrl) ? session.WsUrl : apiUri.Host;
            var port = apiUri.Port != 0 ? $":{apiUri.Port}" : string.Empty;
            return $"{scheme}{wsUrl}{port}/ws?lang=en&token={Uri.EscapeDataString(AuthToken)}&format=protobuf";
        }

        #region Event Handlers
        private Task WebSocketClient_Opened()
        {
            return Task.CompletedTask;
        }

        private async Task WebSocketClient_Ready()
        {
            try
            {
                if (_receivedMessageEvent.HasSubscribers)
                {
                    await _receivedMessageEvent.InvokeAsync(SocketMessageCode.Ready, new Envelope()).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                await Task.CompletedTask;
            }
        }

        private async Task WebSocketClient_ErrorOccurred(Exception exception)
        {
            Console.WriteLine($"WebSocket error occurred: {exception.Message}");
        }

        private async Task WebSocketClient_Closed(Exception ex)
        {
            await DisconnectAsync().ConfigureAwait(false);
            if (_disconnectedEvent.HasSubscribers)
            {
                await _disconnectedEvent.InvokeAsync(ex).ConfigureAwait(false);
            }
        }

        private ValueTask WebSocketClient_MessageReceived(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0)
            {
#if NET6_0_OR_GREATER
                return ValueTask.CompletedTask;
#elif NETSTANDARD2_1
                return new ValueTask();
#endif
            }

            try
            {
                if (_receivedMessageEvent.HasSubscribers)
                {
                    _receivedMessageEvent.InvokeAsync(SocketMessageCode.Data, Envelope.Parser.ParseFrom(data.Span)).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }

#if NET6_0_OR_GREATER
            return ValueTask.CompletedTask;
#elif NETSTANDARD2_1
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
