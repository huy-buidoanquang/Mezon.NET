using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Thread-safe session manager that ensures only one session exists throughout the application's lifecycle.
    /// Uses lock-free reads for hot paths and coalesces concurrent refresh operations.
    /// </summary>
    internal sealed class SessionManager<TOptions> : ISessionManager<TOptions>, IAsyncDisposable where TOptions : MezonOptions
    {
        private static SessionManager<TOptions>? _instance;
        private static readonly object _instanceLock = new object();
        private static volatile bool _isInitialized;

        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
        private readonly IMezonApiClient _apiClient;
        private readonly MezonApiClientOptions _options;
        private readonly Logger _logger;

        private const int RefreshTimeBufferInSeconds = 30;

        private volatile ISession _session;
        private volatile Task<bool>? _refreshTask;
        private volatile bool _autoRefreshSession;
        private int _isDisposed;

        public event Func<ISession, Task>? SessionRefreshed;

        /// <summary>
        /// Gets the singleton instance of SessionManager.
        /// Must call GetOrCreate() before accessing this property.
        /// </summary>
        public static SessionManager<TOptions> Instance
        {
            get
            {
                if (!_isInitialized || _instance == null)
                {
                    throw new InvalidOperationException(
                        "SessionManager has not been initialized. Call GetOrCreate() method first.");
                }

                return _instance;
            }
        }

        /// <summary>
        /// Returns the current auth token without allocations on the hot path.
        /// </summary>
        public string GetToken() => _session.AuthToken;

        /// <summary>
        /// Gets the existing instance or creates a new one if not initialized.
        /// Thread-safe double-checked locking pattern.
        /// </summary>
        public static SessionManager<TOptions> GetOrCreate(MezonApiClientOptions options, LogManager logManager)
        {
            if (_isInitialized && _instance != null)
            {
                return _instance;
            }

            lock (_instanceLock)
            {
                if (_isInitialized && _instance != null)
                {
                    return _instance;
                }

                _instance = new SessionManager<TOptions>(options, logManager);
                _isInitialized = true;
                return _instance;
            }
        }

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

        public static bool IsInitialized => _isInitialized && _instance != null;

        internal SessionManager(MezonApiClientOptions options, LogManager logManager)
        {
            Check.NotNull(options, nameof(options));
            Check.NotNull(logManager, nameof(logManager));
            _options = options;
            _apiClient = new MezonApiClient(_options.RestClientProvider, _options.NetworkTransportProvider, _options);
            _apiClient.ConfigureGatewayBasePath(_options.GatewayBasePath);
            _logger = logManager.CreateLogger("SessionManager");
            _autoRefreshSession = _options.AutoRefreshSession;
            _session = Session.NullSession();
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
                var session = await _apiClient.AuthenticateAppAsync(
                    basicAuthUsername: clientSecret,
                    basicAuthPassword: string.Empty,
                    body: new AppAuthenticationRequest(new AppAccountRequest
                    {
                        AppId = clientId.ToString(),
                        Token = clientSecret
                    })).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(session.Token))
                {
                    _session = new Session(session);
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
            _apiClient.ConfigureGatewayBasePath(_options.GatewayBasePath);
            await LogoutInternalAsync().ConfigureAwait(false);
            await _logger.InfoAsync("Session logged out successfully.").ConfigureAwait(false);
        }

        internal async Task LogoutInternalAsync()
        {
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var currentSession = _session;
                if (string.IsNullOrEmpty(currentSession.AuthToken))
                {
                    return;
                }

                var request = new global::Mezon.Net.Internal.Api.SessionLogoutRequest
                {
                    Token = currentSession.AuthToken,
                    RefreshToken = currentSession.RefreshToken,
                    DeviceId = "",
                    Platform = "",
                };
                await _apiClient.SessionLogoutAsync(request).ConfigureAwait(false);
                _session = Session.NullSession();
            }
            catch (MezonException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MezonAuthenticationException("Logout failed.", ex);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        /// <summary>
        /// Coalesces concurrent refresh calls so only one network request is made.
        /// </summary>
        private Task<bool> TryRefreshSessionAsync()
        {
            var existingTask = _refreshTask;
            if (existingTask != null)
            {
                return existingTask;
            }

            return RefreshAsync();
        }

        private async Task<bool> RefreshAsync()
        {
            await _sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_refreshTask != null)
                {
                    var joined = _refreshTask;
                    _sessionLock.Release();
                    return await joined.ConfigureAwait(false);
                }

                _refreshTask = RefreshInternalAsync();
            }
            finally
            {
                if (_sessionLock.CurrentCount == 0)
                {
                    _sessionLock.Release();
                }
            }

            try
            {
                return await _refreshTask!.ConfigureAwait(false);
            }
            finally
            {
                _refreshTask = null;
            }
        }

        private async Task<bool> RefreshInternalAsync()
        {
            try
            {
                var request = new global::Mezon.Net.Internal.Api.SessionRefreshRequest { Token = _session.RefreshToken };
                var newSession = await _apiClient.RefreshSessionAsync("", "", request).ConfigureAwait(false);

                if (string.IsNullOrEmpty(newSession.Token))
                {
                    _session = Session.NullSession();
                    throw new SessionRefreshFailedException();
                }
                _session = new Session(newSession);
                var handler = SessionRefreshed;
                if (handler != null)
                {
                    await handler.Invoke(_session).ConfigureAwait(false);
                }

                return true;
            }
            catch (SessionRefreshFailedException)
            {
                throw;
            }
            catch (MezonException ex)
            {
                await _logger.ErrorAsync("Session refresh failed with exception.", ex).ConfigureAwait(false);
                throw new SessionRefreshFailedException("Session refresh failed.", ex);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Session refresh failed with exception.", ex).ConfigureAwait(false);
                throw new SessionRefreshFailedException("Session refresh failed.", ex);
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
            await _logger.InfoAsync("SessionManager disposed.").ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed != 0)
            {
                throw new ObjectDisposedException(nameof(SessionManager<TOptions>));
            }
        }
    }
}
