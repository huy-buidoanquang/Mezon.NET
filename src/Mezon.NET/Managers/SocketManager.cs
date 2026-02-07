using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Managers;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Socket;
using Mezon.NET.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mezon.NET.Managers
{
    public class SocketManager : ISocketManager
    {
        private readonly ILogger<ISocketManager> _logger;
        private readonly ILogger<ISocket> _socketLogger;
        private readonly IOptions<MezonApiClientOptions> _mezonApiClientOptions;
        private readonly IOptions<MezonSocketOptions> _mezonSocketOptions;
        protected readonly IWebSocketAdapterFactory _webSocketAdapterFactory;

        protected bool IsHardDisconnect { get; private set; }
        protected IMezonApiClient MezonApiClient { get; private set; }
        protected ISessionManager SessionManager { get; private set; }
        protected ISocket MezonSocket { get; private set; }

        public SocketManager(
            ILogger<ISocketManager> logger,
            ILogger<ISocket> socketLogger,
            IOptions<MezonApiClientOptions> apiClientOptions,
            IOptions<MezonSocketOptions> baseSocketOptions,
            IMezonApiClient mezonApiClient,
            IWebSocketAdapterFactory webSocketAdapter,
            ISessionManager sessionManager)
        {
            _logger = logger;
            _socketLogger = socketLogger;
            _mezonApiClientOptions = apiClientOptions;
            _mezonSocketOptions = baseSocketOptions;
            MezonApiClient = mezonApiClient;
            SessionManager = sessionManager;
            _webSocketAdapterFactory = webSocketAdapter;
        }

        public bool CreateSocket(WebSocketAdapterEnum webSocketAdapter = WebSocketAdapterEnum.Text)
        {
            try
            {
                IWebSocketAdapter adapter = _webSocketAdapterFactory.Create(webSocketAdapter);
                MezonSocket = new MezonSocket(_socketLogger, adapter, _mezonApiClientOptions, _mezonSocketOptions, true);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public async Task ConnectSocketAsync(CancellationToken cancellationToken = default)
        {
            IsHardDisconnect = false;
            await MezonSocket.ConnectAsync(SessionManager.CurrentSession()!, true);
            MezonSocket.MessageTyping += (e) =>
            {
                _logger.LogInformation($"Message: {Json.Serialize(e)}.");
            };
        }

        public Task CloseSocketAsync(bool fireDisconnectEvent, CancellationToken cancellationToken = default)
        {
            IsHardDisconnect = true;
            return MezonSocket.CloseAsync(cancellationToken);
        }

        public Task JoinClanChatAsync(string clanId, CancellationToken cancellationToken = default)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var clanJoinMsg = new ClanJoin()
            {
                ClanJoinDetails = new ClanJoinDetails { ClanId = clanId }
            };
            return MezonSocket.JoinClanChatAsync(clanJoinMsg, cancellationToken);
        }
    }
}
