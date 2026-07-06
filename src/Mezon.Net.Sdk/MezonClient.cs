using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Api;
using Mezon.Net.Client.Managers;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Mmn;
using Mezon.Net.Mmn.Models;
using Mezon.Net.Sdk.Agent;
using Mezon.Net.Sdk.Caching;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient : IAsyncDisposable
    {
        private readonly Client.MezonClient _engine;
        private readonly DmChannelManager _dmChannels = new DmChannelManager();
        private readonly ChannelSendQueue _sendQueue = new ChannelSendQueue();
        private bool _cacheListenersBound;
        private Task? _mmnInitTask;
        private MmnClient? _mmnClient;
        private ZkClient? _zkClient;
        private AgentSseManager? _agentManager;

        public MezonClient(MezonClientOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _engine = new Client.MezonClient(options.ToSocketOptions());
            Clans = new EntityCache<Clan>(options.CacheCapacity);
            Channels = new EntityCache<TextChannel>(options.CacheCapacity);
            Users = new EntityCache<Entities.User>(options.CacheCapacity);
        }

        public MezonClientOptions Options { get; }
        public Client.MezonClient Engine => _engine;
        public IMezonApiClient Api => _engine.ApiClient;
        public DmChannelManager DmChannels => _dmChannels;
        public ChannelSendQueue SendQueue => _sendQueue;
        public EntityCache<Clan> Clans { get; }
        public EntityCache<TextChannel> Channels { get; }
        public EntityCache<Entities.User> Users { get; }

        public string BotId => Options.BotId;
        public ConnectionState ConnectionState => _engine.ConnectionState;
        public long Latency => _engine.Latency;

        public EphemeralKeyPair? KeyGen { get; private set; }
        public string? AddressMmn { get; private set; }
        public ZkProofResult? ZkProofs { get; private set; }

        public event Func<Task>? Ready;

        public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Options.BotId) || string.IsNullOrWhiteSpace(Options.Token))
            {
                throw new InvalidOperationException("BotId and Token are required.");
            }

            if (!await _engine.LoginAsBotInternalAsync(Options.BotId, Options.Token, autoRefreshSession: true).ConfigureAwait(false))
            {
                return false;
            }

            await _engine.ConnectAsync().ConfigureAwait(false);
            await _dmChannels.InitializeAsync(Api, _engine).ConfigureAwait(false);
            await SeedClanCacheAsync(cancellationToken).ConfigureAwait(false);
            BindCacheListeners();
            await InitializeMmnAsync(cancellationToken).ConfigureAwait(false);
            if (Ready != null)
            {
                await Ready.Invoke().ConfigureAwait(false);
            }

            return true;
        }

        public Task ConnectAgentSseAsync(CancellationToken cancellationToken = default)
        {
            _agentManager ??= new AgentSseManager(Options.AgentEventUrl, Options.BotId, Options.Token);
            _agentManager.MessageReceived += async evt =>
            {
                switch (evt.EventType)
                {
                    case "room_started":
                        if (AgentSessionStarted != null) await AgentSessionStarted(evt).ConfigureAwait(false);
                        break;
                    case "room_ended":
                        if (AgentSessionEnded != null) await AgentSessionEnded(evt).ConfigureAwait(false);
                        break;
                    case "room_summary_done":
                        if (AgentSessionSummaryDone != null) await AgentSessionSummaryDone(evt).ConfigureAwait(false);
                        break;
                }
            };
            return _agentManager.ConnectAsync(cancellationToken);
        }

        public async Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
            => await Api.AddQuickMenuAccessAsync(body, options).ConfigureAwait(false);

        public async Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
            => await Api.DeleteQuickMenuAccessAsync(body, options).ConfigureAwait(false);

        public EphemeralKeyPair GetEphemeralKeyPair()
        {
            EnsureMmnClient();
            return _mmnClient!.GenerateEphemeralKeyPair();
        }

        public string GetAddress(long userId) => GetAddress(userId.ToString());

        public string GetAddress(string userId)
        {
            EnsureMmnClient();
            return _mmnClient!.GetAddressFromUserId(userId);
        }

        public Task<ZkProofResult> GetZkProofsAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            EnsureZkClient();
            return _zkClient!.GetZkProofsAsync(request, cancellationToken);
        }

        public Task<NonceResult> GetCurrentNonceAsync(string userId, string tag = "pending", CancellationToken cancellationToken = default)
        {
            EnsureMmnClient();
            var address = GetAddress(userId);
            return _mmnClient!.GetCurrentNonceAsync(address, tag, cancellationToken);
        }

        public async Task<SendTransactionResult> SendTokenAsync(SendTransactionRequest request, CancellationToken cancellationToken = default)
        {
            EnsureMmnClient();
            return await _mmnClient!.SendTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<Clan> GetClanAsync(long clanId, CancellationToken cancellationToken = default)
            => Clans.GetOrFetchAsync(clanId, FetchClanAsync, cancellationToken);

        public ValueTask<TextChannel> GetChannelAsync(long channelId, CancellationToken cancellationToken = default)
            => Channels.GetOrFetchAsync(channelId, FetchChannelAsync, cancellationToken);

        public ValueTask<Entities.User> GetUserAsync(long userId, CancellationToken cancellationToken = default)
            => Users.GetOrFetchAsync(userId, FetchUserAsync, cancellationToken);

        private async ValueTask<Clan> FetchClanAsync(long clanId, CancellationToken cancellationToken)
        {
            var list = await Api.ListClanDescsAsync(new PaginationParams()).ConfigureAwait(false);
            foreach (var clan in list.Clandesc)
            {
                if (clan.ClanId == clanId)
                {
                    return new Clan(this, clan);
                }
            }

            throw new InvalidOperationException($"Clan {clanId} was not found.");
        }

        private async ValueTask<TextChannel> FetchChannelAsync(long channelId, CancellationToken cancellationToken)
        {
            var detail = await Api.GetChannelDetailAsync(channelId).ConfigureAwait(false);
            var clan = await GetClanAsync(detail.ClanId, cancellationToken).ConfigureAwait(false);
            return new TextChannel(this, detail, clan);
        }

        private ValueTask<Entities.User> FetchUserAsync(long userId, CancellationToken cancellationToken)
        {
            _dmChannels.TryGetDmChannelId(userId, out var dmChannelId);
            return new ValueTask<Entities.User>(new Entities.User(this, userId, dmChannelId: dmChannelId));
        }

        private async Task SeedClanCacheAsync(CancellationToken cancellationToken)
        {
            var list = await Api.ListClanDescsAsync(new PaginationParams()).ConfigureAwait(false);
            foreach (var clanDesc in list.Clandesc)
            {
                Clans.Set(clanDesc.ClanId, new Clan(this, clanDesc));
                await _engine.JoinClanChat(clanDesc.ClanId).ConfigureAwait(false);
            }
        }

        private async Task InitializeMmnAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(Options.MmnApiUrl))
            {
                return;
            }

            if (KeyGen != null && AddressMmn != null && ZkProofs != null)
            {
                return;
            }

            if (_mmnInitTask != null)
            {
                await _mmnInitTask.ConfigureAwait(false);
                return;
            }

            _mmnInitTask = InitializeMmnCoreAsync(cancellationToken);
            await _mmnInitTask.ConfigureAwait(false);
        }

        private async Task InitializeMmnCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                EnsureMmnClient();
                var mmn = _mmnClient!;
                KeyGen ??= mmn.GenerateEphemeralKeyPair();
                AddressMmn ??= mmn.GetAddressFromUserId(Options.BotId);

                var session = _engine.CurrentSession;
                var idToken = session.IdToken;
                if (!string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(Options.ZkApiUrl))
                {
                    EnsureZkClient();
                    ZkProofs ??= await _zkClient!.GetZkProofsAsync(new ZkProofRequest
                    {
                        UserId = Options.BotId,
                        Jwt = idToken,
                        Address = AddressMmn,
                        EphemeralPublicKey = KeyGen.PublicKey,
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                _mmnInitTask = null;
                throw;
            }
        }

        private void EnsureMmnClient()
            => _mmnClient ??= new MmnClient(Options.MmnApiUrl, Options.RequestTimeoutMs);

        private void EnsureZkClient()
            => _zkClient ??= new ZkClient(Options.ZkApiUrl, Options.RequestTimeoutMs);

        public async ValueTask DisposeAsync()
        {
            _agentManager?.Dispose();
            _mmnClient?.Dispose();
            _zkClient?.Dispose();
            await _engine.DisposeAsync().ConfigureAwait(false);
        }
    }
}
