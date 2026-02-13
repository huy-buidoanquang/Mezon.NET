using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mezon.NET.Api;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Queue;
using Mezon.Protobuf.Realtime;

namespace Mezon.NET.WebSocket
{
    internal class MezonSocketApiClient : MezonApiClient, IMezonSocketClient, IDisposable, IAsyncDisposable
    {
        public event Func<Envelope, Task> SentMessage { add { _sentMessageEvent.Add(value); } remove { _sentMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Envelope, Task>> _sentMessageEvent = new AsyncEvent<Func<Envelope, Task>>();
        public event Func<Envelope, Task> ReceivedMessageEvent { add { _receivedMessageEvent.Add(value); } remove { _receivedMessageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Envelope, Task>> _receivedMessageEvent = new AsyncEvent<Func<Envelope, Task>>();

        public event Func<Exception, Task> Disconnected { add { _disconnectedEvent.Add(value); } remove { _disconnectedEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();

        private CancellationTokenSource? _connectCancelToken;

        internal IWebSocketClient WebSocketClient { get; }

        public ConnectionState ConnectionState { get; private set; }

        public MezonSocketApiClient(RestClientProvider restClientProvider, GRPCClientProvider grpcClientProvider, WebSocketClientProvider webSocketClientProvider, MezonSocketClientConfiguration configuration)
            : base(restClientProvider, grpcClientProvider, configuration)
        {
            WebSocketClient = webSocketClientProvider();
            WebSocketClient.Opened += WebSocketClient_Opened;
            WebSocketClient.Closed += WebSocketClient_Closed;
            WebSocketClient.ErrorOccurred += WebSocketClient_ErrorOccurred;
            WebSocketClient.BinaryMessageReceived += WebSocketClient_BinaryMessageReceived;
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
        /// <exception cref="InvalidOperationException">The client must be logged in before connecting.</exception>
        /// <exception cref="NotSupportedException">This client is not configured with WebSocket support.</exception>
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

#if DEBUG_PACKETS
                Console.WriteLine("Connecting to gateway: " + _webSocketUrl);
#endif

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
        /// <exception cref="NotSupportedException">This client is not configured with WebSocket support.</exception>
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

        public Task SendAsync(ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
            => SendInternalAsync(bytes, options);

        private async Task SendInternalAsync(ReadOnlyMemory<byte> bytes, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            await RequestQueue.SendAsync(new WebSocketRequest(WebSocketClient, bytes, false, options)).ConfigureAwait(false);
            await _sentMessageEvent.InvokeAsync(new Envelope()).ConfigureAwait(false);
        }

        public async Task JoinClanChat(long clanId, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            options.GatewayBucketType = GatewayBucketType.Unbucketed;
            var bucket = new BucketIds();
            var envelop = new Envelope();
            envelop.ClanJoin = new ClanJoin()
            {
                ClanId = clanId,
            };
            await SendAsync(envelop.ToByteArray(), options);
        }

        public async Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            var payload = new ChannelJoin()
            {
                ClanId = clanId,
                ChannelId = channelId,
                ChannelType = channelType,
                IsPublic = isPublic
            };
            await SendAsync(payload.ToByteArray(), options);
        }

        /// <summary>
        ///     Appends necessary query parameters to the specified gateway URL.
        /// </summary>
        private string GetWebSocketUrl()
        {
            var session = SessionManager.Instance.CurrentSession();
            var apiUri = new Uri(session.ApiUrl!);
            var scheme = apiUri.Scheme == Uri.UriSchemeHttps ? "wss://" : "ws://";
            var wsUrl = !string.IsNullOrEmpty(session.WsUrl) ? session.WsUrl : apiUri.Host;
            var port = apiUri.Port != 0 ? $":{apiUri.Port}" : string.Empty;
            return $"{scheme}{wsUrl}{port}/ws?lang=en&token={Uri.EscapeDataString(AuthToken)}&format=protobuf";
        }

        private Task WebSocketClient_Opened()
        {
            Console.WriteLine("WebSocket connection opened.");
            return Task.CompletedTask;
        }

        private async Task WebSocketClient_ErrorOccurred(Exception exception)
        {
            Console.WriteLine($"WebSocket error occurred: {exception.Message}");
        }

        private Task WebSocketClient_Closed(Exception arg)
        {
            Console.WriteLine("WebSocket connection closed.");
            return Task.CompletedTask;
        }

        private ValueTask WebSocketClient_BinaryMessageReceived(ReadOnlyMemory<byte> data)
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
                var envelop = Envelope.Parser.ParseFrom(data.Span);
                Console.WriteLine(envelop.ToString());
            }
            catch (Exception)
            {
                throw;
            }

#if NET6_0_OR_GREATER
            return ValueTask.CompletedTask;
#elif NETSTANDARD2_1
            return new ValueTask();
#endif
        }

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
