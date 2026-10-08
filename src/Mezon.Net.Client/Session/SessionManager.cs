using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Logging;
using MezonSession = Mezon.Net.Internal.Api.Session;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Thread-safe per-client session manager with coalesced refresh operations.
    /// </summary>
    /// <remarks>
    /// SessionRefresh and SessionLogout are socket APIs (mezon-js sends them as api_request_event), so they go through
    /// the socket client attached with <see cref="AttachSocket"/>. When the socket is down, a bot session is renewed by
    /// authenticating again with the app credentials used at login.
    /// </remarks>
    internal sealed class SessionManager<TOptions> : ISessionManager<TOptions>, IAsyncDisposable where TOptions : MezonOptions
    {
        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
        private readonly IMezonApiClient _apiClient;
        private readonly MezonApiClientOptions _options;
        private readonly Logger _logger;

        private const int RefreshTimeBufferInSeconds = 30;

        private volatile ISession _session;
        private Task<bool>? _refreshTask;
        private volatile bool _autoRefreshSession;
        private int _isDisposed;
        private IMezonApiClient? _socketApi;
        private Func<bool>? _isSocketConnected;
        private AppCredentials? _appCredentials;

        public event Func<ISession, Task>? SessionRefreshed;

        /// <summary>
        /// Returns the current auth token without allocations on the hot path.
        /// </summary>
        public string GetToken() => _session.AuthToken;

        /// <summary>
        /// Returns the current token, refreshing the session if it is about to expire.
        /// Lock-free fast path when the session is still valid.
        /// </summary>
        public async Task<string> GetOrRefreshAsync()
        {
            var currentSession = _session;

            if (!currentSession.IsExpiredSoon(RefreshTimeBufferInSeconds))
            {
                return currentSession.AuthToken;
            }

            if (_autoRefreshSession)
            {
                await TryRefreshSessionAsync().ConfigureAwait(false);
            }

            return _session.AuthToken;
        }

        internal SessionManager(MezonApiClientOptions options, LogManager logManager)
        {
            Check.NotNull(options, nameof(options));
            Check.NotNull(logManager, nameof(logManager));
            _options = options;
            _apiClient = new MezonApiClient(_options.RestClientProvider, _options);
            _apiClient.ConfigureGatewayBasePath(_options.GatewayBasePath);
            _logger = logManager.CreateLogger("SessionManager");
            _autoRefreshSession = _options.AutoRefreshSession;
            _session = Session.NullSession();
        }

        /// <summary>Routes refresh and logout through the socket client while <paramref name="isConnected"/> is true.</summary>
        internal void AttachSocket(IMezonApiClient socketApi, Func<bool> isConnected)
        {
            _socketApi = socketApi ?? throw new ArgumentNullException(nameof(socketApi));
            _isSocketConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
        }

        public ISession CurrentSession() => _session;

        public async Task LoginAsync(long clientId, string clientSecret, bool autoRefreshSession = true)
        {
            ThrowIfDisposed();
            Check.NotNullOrEmpty(clientSecret, nameof(clientSecret));
            _apiClient.ConfigureGatewayBasePath(_options.GatewayBasePath);
            await LoginInternalAsync(clientId, clientSecret, autoRefreshSession).ConfigureAwait(false);
        }

        private async Task LoginInternalAsync(long clientId, string clientSecret, bool autoRefreshSession)
        {
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _autoRefreshSession = autoRefreshSession;
                var credentials = new AppCredentials(clientId, clientSecret);
                var session = await AuthenticateAppAsync(credentials).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(session.Token))
                {
                    _session = new Session(session);
                    _appCredentials = credentials;
                    await _logger.InfoAsync($"Authentication successful. User: {_session.Username}.").ConfigureAwait(false);
                    return;
                }

                _session = Session.NullSession();
                _appCredentials = null;
                throw new MezonAuthenticationException("Authentication failed.");
            }
            catch (MezonException)
            {
                _session = Session.NullSession();
                _appCredentials = null;
                throw;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Authentication failed with exception.", ex).ConfigureAwait(false);
                _session = Session.NullSession();
                _appCredentials = null;
                throw new MezonAuthenticationException("Authentication failed.", ex);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private Task<MezonSession> AuthenticateAppAsync(AppCredentials credentials)
            => _apiClient.AuthenticateAppAsync(
                basicAuthUsername: _options.ServerKey,
                basicAuthPassword: string.Empty,
                body: new AppAuthenticationRequest(new AppAccountRequest
                {
                    AppId = credentials.ClientId.ToString(),
                    Token = credentials.ClientSecret
                }));

        public async Task LoginAsync(ISession session, bool autoRefreshSession = true)
        {
            ThrowIfDisposed();
            _apiClient.ConfigureGatewayBasePath(_options.GatewayBasePath);
            await LoginInternalAsync(session, autoRefreshSession).ConfigureAwait(false);
        }

        private async Task LoginInternalAsync(ISession session, bool autoRefreshSession)
        {
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _autoRefreshSession = autoRefreshSession;
                _appCredentials = null;
                if (session != null && !string.IsNullOrEmpty(session.AuthToken))
                {
                    _session = session;
                    await _logger.InfoAsync($"Authentication successful. User: {_session.Username}.").ConfigureAwait(false);
                    return;
                }

                _session = Session.NullSession();
                throw new MezonAuthenticationException("Authentication failed.");
            }
            catch (MezonException)
            {
                _session = Session.NullSession();
                throw;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Authentication failed with exception.", ex).ConfigureAwait(false);
                _session = Session.NullSession();
                throw new MezonAuthenticationException("Authentication failed.", ex);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task LogoutAsync()
        {
            ThrowIfDisposed();
            await LogoutInternalAsync().ConfigureAwait(false);
            await _logger.InfoAsync("Session logged out successfully.").ConfigureAwait(false);
        }

        /// <summary>
        /// Best-effort server logout over the socket (when connected); the local session is always cleared.
        /// </summary>
        internal async Task LogoutInternalAsync()
        {
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var currentSession = _session;
                _appCredentials = null;
                if (string.IsNullOrEmpty(currentSession.AuthToken))
                {
                    return;
                }

                if (IsSocketConnected)
                {
                    var request = new global::Mezon.Net.Internal.Api.SessionLogoutRequest
                    {
                        Token = currentSession.AuthToken,
                        RefreshToken = currentSession.RefreshToken,
                        DeviceId = "",
                        Platform = "",
                    };

                    try
                    {
                        await _socketApi!.SessionLogoutAsync(request).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await _logger.WarningAsync("Server session logout failed; clearing the local session anyway.", ex).ConfigureAwait(false);
                    }
                }

                _session = Session.NullSession();
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        /// <summary>
        /// Refreshes the session when <paramref name="force"/> is set, or when auto-refresh is on and the token expires
        /// soon. Returns false, without throwing, when no refresh route is available (no socket and no app
        /// credentials).
        /// </summary>
        internal async Task<bool> EnsureFreshAsync(bool force)
        {
            var currentSession = _session;
            if (!force && !(_autoRefreshSession && currentSession.IsExpiredSoon(RefreshTimeBufferInSeconds)))
            {
                return true;
            }

            if (!CanRefresh(currentSession))
            {
                return false;
            }

            return await TryRefreshSessionAsync().ConfigureAwait(false);
        }

        /// <summary>Applies a session the server pushed (RefreshSessionEvent); ignores empty or expired sessions.</summary>
        internal async Task ApplyPushedSessionAsync(ISession session)
        {
            if (string.IsNullOrEmpty(session.AuthToken) || session.IsExpired())
            {
                return;
            }

            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (string.IsNullOrEmpty(_session.AuthToken))
                {
                    // Logged out meanwhile; don't resurrect the session.
                    return;
                }

                _session = session;
            }
            finally
            {
                _sessionLock.Release();
            }

            await RaiseSessionRefreshedAsync(session).ConfigureAwait(false);
        }

        private bool IsSocketConnected => _socketApi != null && _isSocketConnected?.Invoke() == true;

        private bool CanRefresh(ISession session)
            => _appCredentials != null || (IsSocketConnected && !string.IsNullOrEmpty(session.RefreshToken));

        /// <summary>
        /// Coalesces concurrent refresh calls so only one network request is made.
        /// </summary>
        private Task<bool> TryRefreshSessionAsync()
        {
            while (true)
            {
                var existingTask = Volatile.Read(ref _refreshTask);
                if (existingTask != null)
                {
                    return existingTask;
                }

                var refresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref _refreshTask, refresh.Task, null) == null)
                {
                    _ = RunRefreshAsync(refresh);
                    return refresh.Task;
                }
            }
        }

        private async Task RunRefreshAsync(TaskCompletionSource<bool> refresh)
        {
            try
            {
                var refreshed = await RefreshInternalAsync().ConfigureAwait(false);
                Volatile.Write(ref _refreshTask, null);
                refresh.TrySetResult(refreshed);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _refreshTask, null);
                refresh.TrySetException(ex);
            }
        }

        private async Task<bool> RefreshInternalAsync()
        {
            var before = _session;
            MezonSession? renewed = null;
            Exception? lastError = null;

            if (IsSocketConnected && !string.IsNullOrEmpty(before.RefreshToken))
            {
                try
                {
                    var request = new global::Mezon.Net.Internal.Api.SessionRefreshRequest { Token = before.RefreshToken };
                    renewed = await _socketApi!.RefreshSessionAsync(_options.ServerKey, "", request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await _logger.WarningAsync("Session refresh over the socket failed.", ex).ConfigureAwait(false);
                }
            }

            var credentials = _appCredentials;
            if (string.IsNullOrEmpty(renewed?.Token) && credentials != null)
            {
                try
                {
                    renewed = await AuthenticateAppAsync(credentials).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await _logger.ErrorAsync("Session re-authentication failed.", ex).ConfigureAwait(false);
                }
            }

            if (renewed == null || string.IsNullOrEmpty(renewed.Token))
            {
                // Keep the current session: it may still be usable, and clearing it would force a full login.
                throw lastError == null
                    ? new SessionRefreshFailedException()
                    : new SessionRefreshFailedException("Session refresh failed.", lastError);
            }

            // Refresh replies may omit the endpoint URLs; keep the ones from the session being replaced.
            if (string.IsNullOrEmpty(renewed.ApiUrl))
            {
                renewed.ApiUrl = before.ApiUrl ?? string.Empty;
            }

            if (string.IsNullOrEmpty(renewed.WsUrl))
            {
                renewed.WsUrl = before.WsUrl ?? string.Empty;
            }

            if (string.IsNullOrEmpty(renewed.TcpUrl))
            {
                renewed.TcpUrl = before.TcpUrl ?? string.Empty;
            }

            var newSession = new Session(renewed);
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(_session, before))
                {
                    // A login or logout happened meanwhile; it wins over this refresh.
                    return true;
                }

                _session = newSession;
            }
            finally
            {
                _sessionLock.Release();
            }

            await RaiseSessionRefreshedAsync(newSession).ConfigureAwait(false);
            return true;
        }

        private async Task RaiseSessionRefreshedAsync(ISession session)
        {
            var handler = SessionRefreshed;
            if (handler == null)
            {
                return;
            }

            try
            {
                await handler.Invoke(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync("A SessionRefreshed handler failed.", ex).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
            {
                return;
            }

            _sessionLock.Dispose();
            _session = Session.NullSession();
            _appCredentials = null;
            (_apiClient as IDisposable)?.Dispose();
            await _logger.InfoAsync("SessionManager disposed.").ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed != 0)
            {
                throw new ObjectDisposedException(nameof(SessionManager<TOptions>));
            }
        }

        private sealed class AppCredentials
        {
            public AppCredentials(long clientId, string clientSecret)
            {
                ClientId = clientId;
                ClientSecret = clientSecret;
            }

            public long ClientId { get; }
            public string ClientSecret { get; }
        }
    }
}
