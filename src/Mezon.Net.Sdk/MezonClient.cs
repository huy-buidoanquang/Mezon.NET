using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Managers;
using Mezon.Net.Core;
using Mezon.Net.Logging;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Agent;
using Mezon.Net.Sdk.Caching;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient : IAsyncDisposable
    {
        internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new AsyncEvent<Func<LogMessage, Task>>();
        public event Func<LogMessage, Task> Log { add { _logEvent.Add(value); } remove { _logEvent.Remove(value); } }

        private readonly Client.MezonClient _engine;
        private readonly DmChannelManager _dmChannels = new DmChannelManager();
        private readonly ChannelSendQueue _sendQueue = new ChannelSendQueue();
        private bool _cacheListenersBound;
        private AgentSseManager? _agentManager;
        internal readonly Logger _logger;

        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<bool>? _firstConnectTcs;
        private CancellationToken _connectCancellationToken;
        private bool _readyInvoked;

        public MezonClient(MezonClientOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _engine = new Client.MezonClient(options);
            _engine.Log += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);
            _engine.Connected += OnEngineConnectedAsync;
            _logger = _engine.LogManager.CreateLogger("MezonSdkClient");
            Clans = new EntityCache<Clan>(options.CacheCapacity);
            Channels = new EntityCache<TextChannel>(options.CacheCapacity);
            Users = new EntityCache<Entities.User>(options.CacheCapacity);
        }

        public MezonClientOptions Options { get; }
        internal Client.MezonClient Engine => _engine;
        internal Client.MezonApiClient ApiClient => (Client.MezonApiClient)_engine.ApiClient;
        internal DmChannelManager DmChannels => _dmChannels;
        internal ChannelSendQueue SendQueue => _sendQueue;
        public EntityCache<Clan> Clans { get; }
        public EntityCache<TextChannel> Channels { get; }
        public EntityCache<Entities.User> Users { get; }

        public long BotId => Options.BotId;
        public ConnectionState ConnectionState => _engine.ConnectionState;
        public long Latency => _engine.Latency;

        public event Func<Task>? Ready;

        public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
        {
            if (Options.BotId == 0 || string.IsNullOrWhiteSpace(Options.Token))
            {
                throw new ArgumentException("BotId and Token are required.");
            }

            if (!await _engine.LoginAsBotInternalAsync(Options.BotId, Options.Token, autoRefreshSession: true).ConfigureAwait(false))
            {
                return false;
            }

            _connectCancellationToken = cancellationToken;
            _readyInvoked = false;
            _firstConnectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await _engine.ConnectAsync().ConfigureAwait(false);
            await AwaitFirstConnectAsync(_firstConnectTcs.Task, cancellationToken).ConfigureAwait(false);

            return true;
        }

        private static async Task AwaitFirstConnectAsync(Task<bool> connectTask, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                await connectTask.ConfigureAwait(false);
                return;
            }

            var cancelWait = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), cancelWait))
            {
                var completed = await Task.WhenAny(connectTask, cancelWait.Task).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
            }
        }

        private async Task OnEngineConnectedAsync()
        {
            await _initializeGate.WaitAsync(_connectCancellationToken).ConfigureAwait(false);
            try
            {
                await InitializeAfterConnectedAsync(_connectCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _firstConnectTcs?.TrySetException(ex);
                await _logger.ErrorAsync("Failed to initialize after socket connected.", ex).ConfigureAwait(false);
                return;
            }
            finally
            {
                _initializeGate.Release();
            }

            _firstConnectTcs?.TrySetResult(true);

            if (!_readyInvoked)
            {
                _readyInvoked = true;
                if (Ready != null)
                {
                    await Ready.Invoke().ConfigureAwait(false);
                }
            }
        }

        private async Task InitializeAfterConnectedAsync(CancellationToken cancellationToken)
        {
            await _dmChannels.InitializeAsync(_engine).ConfigureAwait(false);
            await SeedClanCacheAsync(cancellationToken).ConfigureAwait(false);
            BindCacheListeners();
            await InitializeMmnAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task ConnectAgentSseAsync(CancellationToken cancellationToken = default)
        {
            _agentManager ??= new AgentSseManager(Options.AgentEventUrl, Options.BotId, Options.Token);
            _agentManager.MessageReceived += async evt =>
            {
                switch (evt.EventType)
                {
                    case "room_started":
                        if (AgentSessionStartedInternal != null)
                        {
                            await AgentSessionStartedInternal(evt).ConfigureAwait(false);
                        }
                        break;
                    case "room_ended":
                        if (AgentSessionEndedInternal != null)
                        {
                            await AgentSessionEndedInternal(evt).ConfigureAwait(false);
                        }
                        break;
                    case "room_summary_done":
                        if (AgentSessionSummaryDoneInternal != null)
                        {
                            await AgentSessionSummaryDoneInternal(evt).ConfigureAwait(false);
                        }
                        break;
                }
            };
            return _agentManager.ConnectAsync(cancellationToken);
        }

        public async Task AddQuickMenuAccessAsync(QuickMenuAccessParams body, RequestOptions? options = null)
            => await _engine.AddQuickMenuAccessAsync(body, options).ConfigureAwait(false);

        public async Task DeleteQuickMenuAccessAsync(QuickMenuAccessParams body, RequestOptions? options = null)
            => await _engine.DeleteQuickMenuAccessAsync(body, options).ConfigureAwait(false);

        public ValueTask<Clan> GetClanAsync(long clanId, CancellationToken cancellationToken = default)
            => Clans.GetOrFetchAsync(clanId, FetchClanAsync, cancellationToken);

        public ValueTask<TextChannel> GetChannelAsync(long channelId, CancellationToken cancellationToken = default)
            => Channels.GetOrFetchAsync(channelId, FetchChannelAsync, cancellationToken);

        public ValueTask<Entities.User> GetUserAsync(long userId, CancellationToken cancellationToken = default)
            => Users.GetOrFetchAsync(userId, FetchUserAsync, cancellationToken);

        private async ValueTask<Clan> FetchClanAsync(long clanId, CancellationToken cancellationToken)
        {
            var list = await _engine.ListClanDescsAsync(new ListClanDescParams()).ConfigureAwait(false);
            for (var i = 0; i < list.Clandesc.Count; i++)
            {
                var clan = list.Clandesc[i].Proto;
                if (clan.ClanId == clanId)
                {
                    return new Clan(this, clan);
                }
            }

            throw new MezonEntityNotFoundException(nameof(Clan), clanId);
        }

        private async ValueTask<TextChannel> FetchChannelAsync(long channelId, CancellationToken cancellationToken)
        {
            var detail = await _engine.GetChannelDetailAsync(channelId).ConfigureAwait(false);
            var clan = await GetClanAsync(detail.ClanId, cancellationToken).ConfigureAwait(false);
            var channel = new TextChannel(this, detail.Proto, clan);
            await channel.JoinAsync().ConfigureAwait(false);
            return channel;
        }

        private ValueTask<Entities.User> FetchUserAsync(long userId, CancellationToken cancellationToken)
        {
            _dmChannels.TryGetDmChannelId(userId, out var dmChannelId);
            return new ValueTask<Entities.User>(new Entities.User(this, userId, dmChannelId: dmChannelId));
        }

        private async Task SeedClanCacheAsync(CancellationToken cancellationToken)
        {
            var list = await _engine.ListClanDescsAsync(new ListClanDescParams()).ConfigureAwait(false);
            for (var i = 0; i < list.Clandesc.Count; i++)
            {
                var clanDesc = list.Clandesc[i].Proto;
                Clans.Set(clanDesc.ClanId, new Clan(this, clanDesc));
                await _engine.JoinClanChatRtAsync(new ClanJoinParams(clanDesc.ClanId)).ConfigureAwait(false);
            }
        }

        partial void DisposeMmn();

        public async ValueTask DisposeAsync()
        {
            _agentManager?.Dispose();
            DisposeMmn();
            await _engine.DisposeAsync().ConfigureAwait(false);
        }
    }
}
