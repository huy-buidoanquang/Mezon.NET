using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Queue;
using Mezon.Net.Utils;
using Newtonsoft.Json;
using MezonSession = Mezon.Net.Internal.Api.Session;
using Stream = System.IO.Stream;

namespace Mezon.Net.Client
{
    internal class MezonApiClient : IMezonApiClient, IDisposable, IAsyncDisposable
    {
        public event Func<string, string, double, Task> ApiSentRequestEvent { add { _apiSentRequestEvent.Add(value); } remove { _apiSentRequestEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, string, double, Task>> _apiSentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();

        protected bool _isDisposed;
        protected readonly JsonSerializer _serializer;
        protected readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _loginCancelToken = new CancellationTokenSource();

        private readonly RestClientProvider _restClientProvider;
        internal RequestQueue RequestQueue { get; }

        public LoginState LoginState { get; private set; }

        internal TokenType TokenType { get; private set; }
        TokenType IMezonApiClient.TokenType => TokenType;

        internal string AuthToken { get; private set; } = string.Empty;
        string IMezonApiClient.AuthToken => AuthToken;

        internal long? CurrentUserId { get; set; }

        long? IMezonApiClient.CurrentUserId => CurrentUserId;

        protected IRestClient RestClient { get; private set; } = default!;

        protected MezonOptions MezonOptions;

        public MezonApiClient(
            RestClientProvider restClientProvider,
            MezonOptions configuration,
            JsonSerializer? serializer = null)
        {
            _restClientProvider = restClientProvider;
            _serializer = serializer ?? Json.Serializer;
            MezonOptions = configuration;
            RequestQueue = new RequestQueue();
            ConfigureGatewayBasePath(configuration.GatewayBasePath);
        }

        public virtual void ConfigureGatewayBasePath(string gatewayBasePath)
        {
            RestClient?.Dispose();
            RestClient = _restClientProvider(gatewayBasePath);
            RestClient.SetHeader("Accept", "*/*");
        }

        internal static string GetPrefixedToken(TokenType tokenType, string token)
        {
            return tokenType switch
            {
                TokenType.Bot => $"Bot {token}",
                TokenType.Bearer => $"Bearer {token}",
                _ => throw new ArgumentException(message: "Unknown OAuth token type.", paramName: nameof(tokenType)),
            };
        }

        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _loginCancelToken?.Dispose();
                    RestClient?.Dispose();
                    RequestQueue?.Dispose();
                    _stateLock?.Dispose();
                }
                _isDisposed = true;
            }
        }

        internal virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _loginCancelToken?.Dispose();
                    RestClient?.Dispose();

                    if (!(RequestQueue is null))
                    {
                        await RequestQueue.DisposeAsync().ConfigureAwait(false);
                    }

                    _stateLock?.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() => Dispose(true);

        public ValueTask DisposeAsync() => DisposeAsync(true);

        public async Task LoginAsync(TokenType tokenType, string token, RequestOptions? options = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LoginInternalAsync(tokenType, token, options).ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task LoginInternalAsync(TokenType tokenType, string token, RequestOptions? options = null)
        {
            if (LoginState != LoginState.LoggedOut)
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }

            LoginState = LoginState.LoggingIn;

            try
            {
                _loginCancelToken?.Dispose();
                _loginCancelToken = new CancellationTokenSource();

                await RequestQueue.SetCancelTokenAsync(_loginCancelToken.Token).ConfigureAwait(false);
                RestClient.SetCancelToken(_loginCancelToken.Token);

                TokenType = tokenType;
                AuthToken = token.TrimEnd();
                if (tokenType != TokenType.Webhook)
                {
                    RestClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
                }

                LoginState = LoginState.LoggedIn;
            }
            catch
            {
                await LogoutInternalAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task LogoutAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task LogoutInternalAsync()
        {
            //An exception here will lock the client into the unusable LoggingOut state, but that's probably fine since our client is in an undefined state too.
            if (LoginState == LoginState.LoggedOut)
            {
                return;
            }

            LoginState = LoginState.LoggingOut;

            try
            {
                _loginCancelToken?.Cancel(false);
            }
            catch { }

            await DisconnectInternalAsync(null).ConfigureAwait(false);
            await RequestQueue.ClearAsync().ConfigureAwait(false);

            await RequestQueue.SetCancelTokenAsync(CancellationToken.None).ConfigureAwait(false);
            RestClient.SetCancelToken(CancellationToken.None);

            CurrentUserId = null;
            LoginState = LoginState.LoggedOut;
        }

        internal virtual Task ConnectInternalAsync() => Task.CompletedTask;

        internal virtual Task DisconnectInternalAsync(Exception? ex = null) => Task.CompletedTask;

        #region Core
        public Task SendNoResAsync(string method, string endpoint, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;

            var request = new ApiRequest(RestClient, method, endpoint, options);
            return SendInternalAsync(method, endpoint, request);
        }

        public Task SendJsonNoResAsync(string method, string endpoint, object payload, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;

            string json = payload != null ? SerializeJson(payload) : string.Empty;
            var request = new JsonApiRequest(RestClient, method, endpoint, json, options);
            return SendInternalAsync(method, endpoint, request);
        }

        public Task SendMultipartNoResAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;

            var request = new MultipartApiRequest(RestClient, method, endpoint, multipartArgs, options);
            return SendInternalAsync(method, endpoint, request);
        }

        public async Task<Stream> SendAsync(string method, string endpoint, RequestOptions? options = null)
        {
            options ??= new RequestOptions();

            var request = new ApiRequest(RestClient, method, endpoint, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        public async Task<Stream> SendJsonAsync(string method, string endpoint, object payload, RequestOptions? options = null)
        {
            options ??= new RequestOptions();

            string json = payload != null ? SerializeJson(payload) : string.Empty;

            var request = new JsonApiRequest(RestClient, method, endpoint, json, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        public async Task<Stream> SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, RequestOptions? options = null)
        {
            options ??= new RequestOptions();

            var request = new MultipartApiRequest(RestClient, method, endpoint, multipartArgs, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        private async Task<Stream> SendInternalAsync(string method, string endpoint, ApiRequest request)
        {
            if (!request.Options.IgnoreState)
            {
                CheckState();
            }

            var stopwatch = Stopwatch.StartNew();
            var responseStream = await RequestQueue.SendAsync(request).ConfigureAwait(false);
            stopwatch.Stop();

            double milliseconds = ToMilliseconds(stopwatch);
            await _apiSentRequestEvent.InvokeAsync(method, endpoint, milliseconds).ConfigureAwait(false);

            return responseStream;
        }

        private static void AddBasicAuthHeader(string? username, string? password, RequestOptions options)
        {
            var basicAuthToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            options.RequestHeaders.Add("Authorization", new[] { $"Basic {basicAuthToken}" });
        }

        protected void CheckState()
        {
            if (LoginState != LoginState.LoggedIn)
            {
                throw new MezonAuthenticationException("Client is not logged in.");
            }
        }

        protected static double ToMilliseconds(Stopwatch stopwatch) => Math.Round((double)stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0, 2);
        #endregion

        protected string SerializeJson(object value)
        {
            var sb = new StringBuilder(256);
            using (TextWriter text = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (JsonWriter writer = new JsonTextWriter(text))
            {
                _serializer.Serialize(writer, value);
            }

            return sb.ToString();
        }

        public void ConfigureApiBasePath(string apiBasePath)
        {
            if (!string.IsNullOrWhiteSpace(apiBasePath))
            {
                ConfigureGatewayBasePath(apiBasePath);
            }
        }

        public Task UpdateAccountAsync(global::Mezon.Net.Internal.Api.UpdateAccountRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendJsonNoResAsync("PUT", "/v2/account", body);
        }

        public async Task<MezonSession> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, global::Mezon.Net.Internal.Api.ConfirmLoginRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return MezonSession.Parser.ParseFrom(await SendJsonAsync("POST", "/v2/account/authenticate/checklogin", body, options: options));
        }

        public Task ConfirmLoginAsync(global::Mezon.Net.Internal.Api.ConfirmLoginRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return SendJsonNoResAsync("POST", "/v2/account/authenticate/confirmlogin", body, options: options);
        }

        public async Task<global::Mezon.Net.Internal.Api.LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, global::Mezon.Net.Internal.Api.LoginRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            return Internal.Api.LoginIDResponse.Parser.ParseFrom(await SendJsonAsync("POST", "/v2/account/authenticate/createqrlogin", body, options: options));
        }

        public async Task<MezonSession> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return MezonSession.Parser.ParseFrom(await SendJsonAsync("POST", "/v2/account/authenticate/email", body, options: options));
        }

        public async Task<MezonSession> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, global::Mezon.Net.Internal.Api.AccountMezon body, Mezon.Net.Client.AccountMezonParams args, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            var queryArgs = new StringBuilder();
            if (args.Create.IsSpecified)
            {
                queryArgs.Append("create=")
                    .Append(args.Create.Value);
            }
            if (args.IsRemember.IsSpecified)
            {
                queryArgs.Append("&is_remember=")
                    .Append(args.IsRemember.Value);
            }
            if (args.Username.IsSpecified)
            {
                queryArgs.Append("&username=")
                    .Append(args.Username.Value);
            }

            return MezonSession.Parser.ParseFrom(await SendJsonAsync("POST", $"/v2/account/authenticate/mezon?{queryArgs}", body, options: options));
        }

        public async Task<LinkAccountConfirmRequest> AuthenticateSMSOTPAsync(string basicAuthUsername, string basicAuthPassword, AuthenticateSMSRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return LinkAccountConfirmRequest.Parser.ParseFrom(await SendJsonAsync("POST", "/v2/account/authenticate/emailotp", body, options: options));
        }

        public async Task<MezonSession> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return MezonSession.Parser.ParseFrom(await SendJsonAsync("POST", "/v2/apps/authenticate/token", body, options: options));
        }

        #region Socket API stubs

        public virtual async Task<MezonSession> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, global::Mezon.Net.Internal.Api.SessionRefreshRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ClanDescList> ListClanDescsAsync(ListClanDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteAccountAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<Account> GetAccountAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteClanDescAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<Internal.Api.ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteRoleAsync(long roleId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<RoleListEventResponse> ListRolesAsync(RoleListEventRequest request, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateUserAsync(UpdateUsersRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteEventAsync(long eventId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelMessage> CreatePinMessageAsync(PinMessageRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task MarkAsReadAsync(MarkAsReadRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateClanEmojiByIdAsync(ClanEmojiUpdateRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateClanStickerByIdAsync(ClanStickerUpdateByIdRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateWebhookByIdAsync(WebhookUpdateRequestById body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteWebhookByIdAsync(WebhookDeleteRequestById body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetChanEncryptionMethodAsync(ChanEncryptionMethod body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task PushPublicKeyAsync(PushPubKeyRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListOnboardingResponse> CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateOnboardingAsync(UpdateOnboardingRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListUserActivity> ListActivityAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<PermissionList> GetListPermissionAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<RoleUserList> ListRoleUsersAsync(ListRoleUsersRequest request, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? category = null, int? direction = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CategoryDesc> CreateCategoryDescAsync(CreateCategoryDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateCategoryAsync(UpdateCategoryDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetNotificationChannelSettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetMuteNotificationCategoryAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetMuteNotificationChannelAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UserStatus> GetUserStatusAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateUserStatusAsync(UserStatusUpdate body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<App> GetAppAsync(long id, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<App> UpdateAppAsync(UpdateAppRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddUserEventAsync(UserEventRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task HealthcheckAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelDescList> ListChannelDescsAsync(ListChannelDescsRequest request, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<Internal.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(RegistFcmDeviceTokenRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateUserProfileByClanAsync(UpdateClanProfileRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task LeaveThreadAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelDescList> SearchThreadAsync(SearchThreadRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<LinkAccountConfirmRequest> LinkSMSAsync(AccountMezon body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ConfirmLinkMezonOTPAsync(LinkAccountConfirmRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<LinkAccountConfirmRequest> LinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UnlinkMezonAsync(AccountMezon body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UnlinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddRolesChannelDescAsync(AddRoleChannelDescRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetRoleChannelPermissionAsync(UpdateRoleChannelRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(PermissionRoleChannelListEventRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<NotificationUserChannel> GetNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<NotificationUserChannel> GetNotificationCategoryAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetNotificationCategorySettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteNotificationCategorySettingAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelMessage> CreateMessage2InboxAsync(Message2InboxRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateChannelPrivateAsync(ChangeChannelPrivateRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ChangeChannelCategoryAsync(ChangeChannelCategoryRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<AllUsersAddChannelResponse> ListChannelUsersUCAsync(AllUsersAddChannelRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<EditChannelCanvasResponse> EditChannelCanvasesAsync(EditChannelCanvasRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<AddFavoriteChannelResponse> AddChannelFavoriteAsync(AddFavoriteChannelRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GenerateClanWebhookResponse> GenerateClanWebhookAsync(GenerateClanWebhookRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateClanWebhookByIdAsync(UpdateClanWebhookRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateOnboardingStepAsync(UpdateOnboardingStepRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<IsFollowerResponse> IsFollowerAsync(IsFollowerRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelMessageAck> SendChannelMessageAsync(ChannelMessageSend body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelMessageAck> SendChannelMessageAsync(Mezon.Net.Models.SendChannelMessageParams message, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateChannelMessageAsync(ChannelMessageUpdate body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteChannelMessageAsync(ChannelMessageRemove body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task RemoveParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task MuteParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CreateRoomChannelApps> CreateRoomChannelAppsAsync(CreateRoomChannelApps body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(GenerateHashChannelAppsRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<MezonOauthClient> GetMezonOauthClientAsync(GetMezonOauthClientRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<MezonOauthClient> UpdateMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<SdTopicList> ListSdTopicAsync(ListSdTopicRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<SdTopic> GetTopicDetailAsync(SdTopicDetailRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<SdTopic> CreateSdTopicAsync(SdTopicRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DeleteSdTopicAsync(DeleteSdTopicRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task MessageButtonClickAsync(MessageButtonClicked body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DropdownBoxSelectedAsync(DropdownBoxSelected body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ActiveArchivedThreadAsync(ActiveArchivedThread body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task AddAgentToChannelAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task DisconnectAgentAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ReportMessageAbuseAsync(ReportMessageAbuseReqest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<StreamHttpCallbackResponse> StreamingServerCallbackAsync(StreamHttpCallbackRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ForSaleItemList> ListForSaleItemsAsync(ListForSaleItemsRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task HandleClanWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<MutedChannelList> ListMutedChannelAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListClanBadgeCountResponse> ListClanBadgeCountAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListChannelBadgeCountResponse> ListChannelBadgeCountAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<LogedDeviceList> ListLogedDeviceAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ClanUserStatusList> ListClanUsersStatusAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListChannelTimelineResponse> ListChannelTimelineAsync(ListChannelTimelineRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListArchivedChannelDescsResponse> ListArchivedChannelDescsAsync(long clanId, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ListUserOnlineResponse> ListUserOnlineAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<global::Mezon.Net.Internal.Api.Session> RegistrationEmailAsync(global::Mezon.Net.Internal.Api.RegistrationEmailRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UploadAttachment> UploadAttachmentFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UploadAttachment> UploadOauthFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<Role> CreateRoleAsync(global::Mezon.Net.Internal.Api.CreateRoleRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<EventManagement> CreateEventAsync(global::Mezon.Net.Internal.Api.CreateEventRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ArchiveChannelAsync(ArchiveChannelRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<LinkInviteUser> CreateLinkInviteUserAsync(global::Mezon.Net.Internal.Api.LinkInviteUserRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SetNotificationClanSettingAsync(global::Mezon.Net.Internal.Api.SetDefaultNotificationRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateAccountAsync(Internal.Api.UpdateAccountRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<global::Mezon.Net.Internal.Api.Session> UpdateUsernameAsync(UpdateUsernameRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateCategoryOrderAsync(global::Mezon.Net.Internal.Api.UpdateCategoryOrderRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateRoleAsync(global::Mezon.Net.Internal.Api.UpdateRoleRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateEventAsync(global::Mezon.Net.Internal.Api.UpdateEventRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<global::Mezon.Net.Internal.Api.SearchMessageResponse> SearchMessageAsync(global::Mezon.Net.Internal.Api.SearchMessageRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task HandleWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CheckDuplicateNameResponse> CheckDuplicateNameAsync(CheckDuplicateNameRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<App> AddAppAsync(global::Mezon.Net.Internal.Api.AddAppRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UserActivity> CreateActivityAsync(global::Mezon.Net.Internal.Api.CreateActivityRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task UpdateUserCustomStatusAsync(User body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<global::Mezon.Net.Internal.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UpdateChannelTimelineResponse> UpdateChannelTimelineAsync(UpdateChannelTimelineRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CreateChannelTimelineResponse> CreateChannelTimelineAsync(CreateChannelTimelineRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<ChannelTimelineDetailResponse> DetailChannelTimelineAsync(ChannelTimelineDetailRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<CreatePollResponse> CreatePollAsync(CreatePollRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<VotePollResponse> VotePollAsync(VotePollRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ClosePollAsync(ClosePollRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<GetPollResponse> GetPollAsync(GetPollRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task ReactChannelMessageAsync(MessageReaction body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<MultipartUploadAttachment> MultipartUploadAttachmentFileStartAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UploadAttachment> MultipartUploadAttachmentFileFinishAsync(MultipartUploadAttachmentFinishRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task SessionLogoutAsync(SessionLogoutRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        public virtual Task<UploadAttachmentBatch> UploadBatchAttachmentFileAsync(UploadBatchAttachmentRequest body, RequestOptions? options = null)
        {
            throw new NotSupportedException("Socket API is not available on REST-only client.");
        }

        #endregion
    }
}
