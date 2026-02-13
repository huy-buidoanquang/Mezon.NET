using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Logging;

namespace Mezon.NET.Api
{
    /// <summary>
    /// Thread-safe singleton session manager that ensures only one session exists throughout the application's lifecycle.
    /// Uses lazy initialization for efficient resource usage.
    /// </summary>
    internal sealed class SessionManager : IDisposable, IAsyncDisposable
    {
        private static SessionManager? _instance;
        private static readonly object _instanceLock = new object();
        private static volatile bool _isInitialized;

        internal readonly Logger _logger;
        internal LogManager LogManager { get; }
        internal readonly AsyncEvent<Func<string, string, double, Task>> _sentRequest = new AsyncEvent<Func<string, string, double, Task>>();

        private const int RefreshTimeBufferInSeconds = 30;
        private readonly SemaphoreSlim _sessionRefreshLock = new SemaphoreSlim(1, 1);
        private Timer? _sessionCheckTimer;
        private volatile ISession _session;
        private bool _isDisposed;
        private readonly IMezonApiClient _apiClient;
        private readonly MezonConfiguration _mezonConfiguration;
        private bool _autoRefreshSession;

        /// <summary>
        /// Gets the singleton instance of SessionManager.
        /// Must call Initialize() or GetOrCreate() before accessing this property.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if Initialize() has not been called</exception>
        public static SessionManager Instance
        {
            get
            {
                if (!_isInitialized || _instance == null)
                {
                    throw new InvalidOperationException(
                        "SessionManager has not been initialized. Call Initialize() or GetOrCreate() method first.");
                }

                return _instance;
            }
        }

        /// <summary>
        /// Initializes the singleton instance with required dependencies.
        /// This method is thread-safe and can only be called once.
        /// </summary>
        /// <param name="mezonConfiguration">The Mezon configuration</param>
        /// <param name="apiClient">The API client instance</param>
        /// <returns>The initialized SessionManager instance</returns>
        /// <exception cref="InvalidOperationException">Thrown if already initialized</exception>
        public static SessionManager Initialize(MezonConfiguration mezonConfiguration, IMezonApiClient apiClient)
        {
            if (mezonConfiguration == null)
            {
                throw new ArgumentNullException(nameof(mezonConfiguration));
            }

            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            lock (_instanceLock)
            {
                if (_isInitialized && _instance != null)
                {
                    throw new InvalidOperationException("SessionManager has already been initialized.");
                }

                _instance = new SessionManager(mezonConfiguration, apiClient);
                _isInitialized = true;
                return _instance;
            }
        }

        /// <summary>
        /// Gets the existing instance or creates a new one if not initialized.
        /// This method is thread-safe and idempotent.
        /// </summary>
        /// <param name="mezonConfiguration">The Mezon configuration (used only if creating new instance)</param>
        /// <param name="apiClient">The API client instance (used only if creating new instance)</param>
        /// <returns>The SessionManager instance</returns>
        public static SessionManager GetOrCreate(MezonConfiguration mezonConfiguration, IMezonApiClient apiClient)
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

                _instance = new SessionManager(mezonConfiguration, apiClient);
                _isInitialized = true;
                return _instance;
            }
        }

        /// <summary>
        /// Checks if the SessionManager has been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized && _instance != null;

        /// <summary>
        /// Resets the singleton instance. Use with caution - primarily for testing purposes.
        /// </summary>
        internal static void Reset()
        {
            lock (_instanceLock)
            {
                _instance?.Dispose();
                _instance = null;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// Use Initialize() or GetOrCreate() method to create the instance.
        /// </summary>
        private SessionManager(MezonConfiguration mezonConfiguration, IMezonApiClient apiClient)
        {
            _mezonConfiguration = mezonConfiguration ?? throw new ArgumentNullException(nameof(mezonConfiguration));
            LogManager = new LogManager(mezonConfiguration.LogLevel);
            _logger = LogManager.CreateLogger("SessionManager");
            _autoRefreshSession = true;
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _session = Session.NullSession();

            _apiClient.SentRequest += async (method, endpoint, millis) => await _logger.VerboseAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false);
            _apiClient.SentRequest += (method, endpoint, millis) => _sentRequest.InvokeAsync(method, endpoint, millis);
        }

        /// <summary>
        /// Gets the current session instance.
        /// Thread-safe access to the session object.
        /// </summary>
        /// <returns>The current session or a null session if not authenticated</returns>
        public ISession CurrentSession() => _session;

        /// <summary>
        /// Creates a new session with the stored token.
        /// This ensures only one session is active at a time.
        /// </summary>
        /// <param name="autoRefreshSession">Whether to automatically refresh the session when it expires</param>
        /// <exception cref="InvalidOperationException">Thrown if authentication fails</exception>
        public async Task LoginAsync(bool autoRefreshSession = true)
        {
            _apiClient.ConfigureGatewayBasePath(_mezonConfiguration.GatewayBasePath);
            var success = await LoginInternalAsync(autoRefreshSession).ConfigureAwait(false);
            if (!success)
            {
                throw new InvalidOperationException("Authentication failed, API session is null.");
            }

            await _logger.InfoAsync("Session created successfully.").ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new session with the stored token.
        /// This ensures only one session is active at a time.
        /// </summary>
        /// <param name="autoRefreshSession">Whether to automatically refresh the session when it expires</param>
        /// <exception cref="InvalidOperationException">Thrown if authentication fails</exception>
        public async Task LoginAsync(ISession session, bool autoRefreshSession = true)
        {
            _apiClient.ConfigureGatewayBasePath(_mezonConfiguration.GatewayBasePath);
            var success = await LoginInternalAsync(session, autoRefreshSession).ConfigureAwait(false);
            if (!success)
            {
                throw new InvalidOperationException("Authentication failed, API session is null.");
            }

            await _logger.InfoAsync("Session created successfully.").ConfigureAwait(false);
        }

        /// <summary>
        /// Authenticates with the provided token and creates a new session.
        /// Thread-safe operation that ensures only one authentication process runs at a time.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="autoRefreshSession">Whether to enable automatic session refresh</param>
        /// <returns>True if authentication was successful, false otherwise</returns>
        internal async Task<bool> LoginInternalAsync(bool autoRefreshSession = true)
        {
            await _sessionRefreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                StopSessionTimer();
                _autoRefreshSession = autoRefreshSession;

                await _logger.InfoAsync($"Authenticating with token... (Auto-refresh: {autoRefreshSession})").ConfigureAwait(false);

                //var session = await _apiClient.AuthenticateAppAsync(
                //    basicAuthUsername: _mezonConfiguration.ClientSecret,
                //    basicAuthPassword: string.Empty,
                //    body: new AppAuthenticationRequest(new AppAccountRequest { AppId = _mezonConfiguration.ClientId, Token = _mezonConfiguration.ClientSecret })
                //).ConfigureAwait(false);

                //if (session != null && !string.IsNullOrEmpty(session.Token))
                //{
                //    _session = new Session(session);
                //    StartSessionTimer();
                //    await _logger.InfoAsync($"Authentication successful. User: {_session.Username}, Expires: {DateTimeOffset.FromUnixTimeSeconds(_session.ExpiresAt)}").ConfigureAwait(false);
                //    return true;
                //}

                await _logger.WarningAsync("Authentication failed: Invalid response from API.").ConfigureAwait(false);
                _session = Session.NullSession();
                return false;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Authentication failed with exception.", ex).ConfigureAwait(false);
                _session = Session.NullSession();
                return false;
            }
            finally
            {
                _sessionRefreshLock.Release();
            }
        }

        /// <summary>
        /// Authenticates with the provided token and creates a new session.
        /// Thread-safe operation that ensures only one authentication process runs at a time.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="autoRefreshSession">Whether to enable automatic session refresh</param>
        /// <returns>True if authentication was successful, false otherwise</returns>
        internal async Task<bool> LoginInternalAsync(ISession session, bool autoRefreshSession = true)
        {
            await _sessionRefreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                StopSessionTimer();
                _autoRefreshSession = autoRefreshSession;

                await _logger.InfoAsync($"Authenticating with token... (Auto-refresh: {autoRefreshSession})").ConfigureAwait(false);

                if (session != null && !string.IsNullOrEmpty(session.AuthToken))
                {
                    _session = session;
                    StartSessionTimer();
                    await _logger.InfoAsync($"Authentication successful. User: {_session.Username}, Expires: {DateTimeOffset.FromUnixTimeSeconds(_session.ExpiresAt)}").ConfigureAwait(false);
                    return true;
                }

                await _logger.WarningAsync("Authentication failed: Invalid response from API.").ConfigureAwait(false);
                _session = Session.NullSession();
                return false;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Authentication failed with exception.", ex).ConfigureAwait(false);
                _session = Session.NullSession();
                return false;
            }
            finally
            {
                _sessionRefreshLock.Release();
            }
        }

        /// <summary>
        /// Logs out the current session and clears session data.
        /// Thread-safe operation that ensures clean session termination.
        /// </summary>
        /// <returns>True if logout was successful</returns>
        public async Task LogoutAsync()
        {
            _apiClient.ConfigureGatewayBasePath(_mezonConfiguration.GatewayBasePath);
            var success = await LogoutInternalAsync().ConfigureAwait(false);
            if (!success)
            {
                throw new InvalidOperationException("Logout failed.");
            }

            await _logger.InfoAsync("Session logged out successfully.").ConfigureAwait(false);
        }

        internal async Task<bool> LogoutInternalAsync()
        {
            await _sessionRefreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                StopSessionTimer();
                if (_session == null)
                {
                    return true;
                }

                var response = await _apiClient.AuthenticateAppLogoutAsync(
                    new AppAuthenticationLogoutRequest
                    {
                        Token = _session.AuthToken,
                        RefreshToken = _session.RefreshToken
                    }
                ).ConfigureAwait(false);
                if (!response)
                {
                    return false;
                }
                _session = Session.NullSession();
                return true;
            }
            finally
            {
                _sessionRefreshLock.Release();
            }
        }

        private async Task RefreshSessionIfNeededAsync(CancellationToken cancellationToken)
        {
            if (!_autoRefreshSession || _session == null || !_session.IsExpiredSoon(RefreshTimeBufferInSeconds))
            {
                return;
            }

            await _sessionRefreshLock.WaitAsync(cancellationToken);
            try
            {
                if (!_session.IsExpiredSoon(RefreshTimeBufferInSeconds))
                {
                    return;
                }

                var request = new SessionRefreshRequest { Token = _session.RefreshToken };
                var newSession = await _apiClient.RefreshSessionAsync("", "", request);

                if (newSession != null && !string.IsNullOrEmpty(newSession.Token))
                {
                    _session = new Session(newSession);
                }
                else
                {
                    _session = Session.NullSession();
                    StopSessionTimer();
                    throw new SessionRefreshFailedException();
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("An error occurred while refreshing the session.", ex).ConfigureAwait(false);
                _session = Session.NullSession();
            }
            finally
            {
                _sessionRefreshLock.Release();
            }
        }

        private void StartSessionTimer()
        {
            if (!_autoRefreshSession)
            {
                return;
            }

            _sessionCheckTimer?.Dispose();

            _sessionCheckTimer = new Timer(async (e) => await RefreshSessionIfNeededAsync(CancellationToken.None), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private void StopSessionTimer()
        {
            _sessionCheckTimer?.Dispose();
            _sessionCheckTimer = null;
        }

        /// <summary>
        /// Disposes the SessionManager and releases all resources.
        /// Thread-safe cleanup of session resources.
        /// </summary>
        internal void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                StopSessionTimer();
                _sessionRefreshLock?.Dispose();
                _session = Session.NullSession();

                // Note: In a true singleton, we typically don't reset the instance,
                // but we clean up resources
                _logger?.InfoAsync("SessionManager disposed.").GetAwaiter().GetResult();
            }

            _isDisposed = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously disposes the SessionManager and releases all resources.
        /// Thread-safe cleanup of session resources.
        /// </summary>
        internal async ValueTask DisposeAsync(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                StopSessionTimer();
                _sessionRefreshLock?.Dispose();
                _session = Session.NullSession();

                await _logger.InfoAsync("SessionManager disposed asynchronously.").ConfigureAwait(false);
            }

            _isDisposed = true;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
