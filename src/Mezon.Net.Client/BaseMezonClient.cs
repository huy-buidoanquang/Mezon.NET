using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Logging;

namespace Mezon.Net.Client
{
    public abstract class BaseMezonClient : IMezonClient
    {
        internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new AsyncEvent<Func<LogMessage, Task>>();
        public event Func<LogMessage, Task> Log { add { _logEvent.Add(value); } remove { _logEvent.Remove(value); } }

        public event Func<Task> LoggedIn { add { _loggedInEvent.Add(value); } remove { _loggedInEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedInEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> LoggedOut { add { _loggedOutEvent.Add(value); } remove { _loggedOutEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedOutEvent = new AsyncEvent<Func<Task>>();

        internal readonly AsyncEvent<Func<string, string, double, Task>> _apiSentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();
        /// <summary>
        ///     Fired when a REST request is sent to the API. First parameter is the HTTP method,
        ///     second is the endpoint, and third is the time taken to complete the request.
        /// </summary>
        public event Func<string, string, double, Task> ApiSentRequestEvent { add { _apiSentRequestEvent.Add(value); } remove { _apiSentRequestEvent.Remove(value); } }

        private readonly Logger _logger;
        protected readonly SemaphoreSlim StateLock;
        private bool _isFirstLogin, _isDisposed;

        private readonly ISessionManager<MezonApiClientOptions> _sessionManager;

        protected ISessionManager<MezonApiClientOptions> SessionManager => _sessionManager;

        protected readonly MezonApiClientOptions Options;

        public IMezonApiClient ApiClient { get; }

        internal LogManager LogManager { get; }
        /// <summary>
        ///     Gets the login state of the client.
        /// </summary>
        public LoginState LoginState { get; private set; }

        public TokenType TokenType => ApiClient.TokenType;

        public virtual ConnectionState ConnectionState => ConnectionState.Disconnected;

        internal BaseMezonClient(MezonApiClientOptions options, IMezonApiClient apiClient)
        {
            Options = options;
            ApiClient = apiClient;

            LogManager = new LogManager(Options.LogLevel);
            LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);
            _logger = LogManager.CreateLogger("MezonClient");

            StateLock = new SemaphoreSlim(1, 1);
            _isFirstLogin = Options.DisplayInitialLog;
            _sessionManager = SessionManager<MezonApiClientOptions>.GetOrCreate(options, LogManager);

            ApiClient.ApiSentRequestEvent += async (method, endpoint, millis) => await _logger.DebugAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false);
            ApiClient.ApiSentRequestEvent += (method, endpoint, millis) => _apiSentRequestEvent.InvokeAsync(method, endpoint, millis);
        }

        public virtual async Task<bool> LoginAsync(ISession session)
        {
            await StateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _sessionManager.LoginAsync(session).ConfigureAwait(false);
                if (_sessionManager.CurrentSession().IsExpired())
                {
                    return false;
                }

                await LoginInternalAsync(TokenType, _sessionManager.CurrentSession().AuthToken).ConfigureAwait(false);
                return true;
            }
            catch (MezonException)
            {
                throw;
            }
            catch
            {
                return false;
            }
            finally
            {
                StateLock.Release();
            }
        }

        public virtual Task<bool> LoginAsync()
        {
            var session = _sessionManager.CurrentSession();
            return string.IsNullOrEmpty(session.AuthToken)
                ? Task.FromResult(false)
                : LoginAsync(session);
        }

        internal virtual async Task LoginInternalAsync(TokenType tokenType, string token)
        {
            if (_isFirstLogin)
            {
                _isFirstLogin = false;
                await LogManager.WriteInitialLog().ConfigureAwait(false);
            }

            if (LoginState != LoginState.LoggedOut)
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }

            LoginState = LoginState.LoggingIn;

            try
            {
                ApiClient.ConfigureApiBasePath(_sessionManager.CurrentSession().ApiUrl ?? string.Empty);
                await ApiClient.LoginAsync(tokenType, token).ConfigureAwait(false);
                await OnLoginAsync(tokenType, token).ConfigureAwait(false);
                LoginState = LoginState.LoggedIn;
            }
            catch
            {
                await LogoutInternalAsync().ConfigureAwait(false);
                throw;
            }

            await _loggedInEvent.InvokeAsync().ConfigureAwait(false);
        }

        internal virtual Task OnLoginAsync(TokenType tokenType, string token) => Task.CompletedTask;

        public async Task LogoutAsync()
        {
            await StateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                StateLock.Release();
            }
        }

        internal virtual async Task LogoutInternalAsync()
        {
            if (LoginState == LoginState.LoggedOut)
            {
                return;
            }

            LoginState = LoginState.LoggingOut;

            await _sessionManager.LogoutAsync().ConfigureAwait(false);
            await ApiClient.LogoutAsync().ConfigureAwait(false);

            await OnLogoutAsync().ConfigureAwait(false);
            LoginState = LoginState.LoggedOut;

            await _loggedOutEvent.InvokeAsync().ConfigureAwait(false);
        }

        internal virtual Task OnLogoutAsync() => Task.CompletedTask;

        /// <inheritdoc />
        Task IMezonClient.ConnectAsync()
            => Task.CompletedTask;
        /// <inheritdoc />
        Task IMezonClient.DisconnectAsync()
            => Task.CompletedTask;

        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                StateLock?.Dispose();
                _isDisposed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        internal virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!_isDisposed)
            {
                StateLock?.Dispose();
                _isDisposed = true;
            }
        }

        public ValueTask DisposeAsync() => DisposeAsync(true);
    }
}
