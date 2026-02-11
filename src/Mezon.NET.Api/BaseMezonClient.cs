using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Logging;

namespace Mezon.NET.Api
{
    public abstract class BaseMezonClient : IMezonClient
    {
        internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new AsyncEvent<Func<LogMessage, Task>>();
        public event Func<LogMessage, Task> Log { add { _logEvent.Add(value); } remove { _logEvent.Remove(value); } }

        public event Func<Task> LoggedIn { add { _loggedInEvent.Add(value); } remove { _loggedInEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedInEvent = new AsyncEvent<Func<Task>>();

        public event Func<Task> LoggedOut { add { _loggedOutEvent.Add(value); } remove { _loggedOutEvent.Remove(value); } }
        private readonly AsyncEvent<Func<Task>> _loggedOutEvent = new AsyncEvent<Func<Task>>();

        internal readonly AsyncEvent<Func<string, string, double, Task>> _sentRequest = new AsyncEvent<Func<string, string, double, Task>>();
        /// <summary>
        ///     Fired when a REST request is sent to the API. First parameter is the HTTP method,
        ///     second is the endpoint, and third is the time taken to complete the request.
        /// </summary>
        public event Func<string, string, double, Task> SentRequest { add { _sentRequest.Add(value); } remove { _sentRequest.Remove(value); } }

        internal readonly Logger _logger;
        private readonly SemaphoreSlim _stateLock;
        private readonly MezonConfiguration _configuration;
        private bool _isFirstLogin, _isDisposed;

        internal IMezonApiClient ApiClient { get; }
        internal LogManager LogManager { get; }
        /// <summary>
        ///     Gets the login state of the client.
        /// </summary>
        public LoginState LoginState { get; private set; }

        public TokenType TokenType => ApiClient.TokenType;

        public ConnectionState ConnectionState => throw new System.NotImplementedException();

        public MezonConfiguration ClientConfiguration => _configuration;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected BaseMezonClient(MezonConfiguration mezonConfiguration)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }

        internal BaseMezonClient(MezonApiClientConfiguration mezonConfiguration, IMezonApiClient apiClient)
        {
            ApiClient = apiClient;
            LogManager = new LogManager(mezonConfiguration.LogLevel);
            SessionManager.GetOrCreate(mezonConfiguration, apiClient);
            LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);

            _stateLock = new SemaphoreSlim(1, 1);
            _logger = LogManager.CreateLogger("MezonClient");
            _isFirstLogin = mezonConfiguration.DisplayInitialLog;
            _configuration = mezonConfiguration;

            ApiClient.RequestQueue.RateLimitTriggered += async (id, info, endpoint) =>
            {
                if (info == null)
                {
                    await _logger.VerboseAsync($"Preemptive Rate limit triggered: {endpoint} {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
                }
                else
                {
                    await _logger.WarningAsync($"Rate limit triggered: {endpoint} Remaining: {info.Value.RetryAfter}s {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
                }
            };
            ApiClient.SentRequest += async (method, endpoint, millis) => await _logger.VerboseAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false);
            ApiClient.SentRequest += (method, endpoint, millis) => _sentRequest.InvokeAsync(method, endpoint, millis);
        }

        internal BaseMezonClient(MezonApiClientConfiguration mezonConfiguration, IMezonApiClient apiClient, LogManager logManager)
        {
            ApiClient = apiClient;
            LogManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            SessionManager.GetOrCreate(mezonConfiguration, apiClient);
            LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);

            _stateLock = new SemaphoreSlim(1, 1);
            _logger = LogManager.CreateLogger("MezonClient");
            _isFirstLogin = mezonConfiguration.DisplayInitialLog;
            _configuration = mezonConfiguration;

            ApiClient.RequestQueue.RateLimitTriggered += async (id, info, endpoint) =>
            {
                if (info == null)
                {
                    await _logger.VerboseAsync($"Preemptive Rate limit triggered: {endpoint} {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
                }
                else
                {
                    await _logger.WarningAsync($"Rate limit triggered: {endpoint} Remaining: {info.Value.RetryAfter}s {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
                }
            };
            ApiClient.SentRequest += async (method, endpoint, millis) => await _logger.VerboseAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false);
            ApiClient.SentRequest += (method, endpoint, millis) => _sentRequest.InvokeAsync(method, endpoint, millis);
        }

        public virtual async Task<bool> LoginAsync(ISession session)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SessionManager.Instance.LoginAsync(session).ConfigureAwait(false);
                if (SessionManager.Instance.CurrentSession().IsExpired())
                {
                    return false;
                }

                await LoginInternalAsync(TokenType, SessionManager.Instance.CurrentSession().AuthToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public virtual async Task<bool> LoginAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SessionManager.Instance.LoginAsync().ConfigureAwait(false);
                if (SessionManager.Instance.CurrentSession().IsExpired())
                {
                    return false;
                }

                await LoginInternalAsync(TokenType, SessionManager.Instance.CurrentSession().AuthToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _stateLock.Release();
            }
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
                ApiClient.ConfigureApiBasePath(SessionManager.Instance.CurrentSession().ApiUrl ?? string.Empty);
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

        internal virtual async Task LogoutInternalAsync()
        {
            if (LoginState == LoginState.LoggedOut)
            {
                return;
            }

            LoginState = LoginState.LoggingOut;

            await SessionManager.Instance.LogoutAsync().ConfigureAwait(false);
            await ApiClient.LogoutAsync().ConfigureAwait(false);

            await OnLogoutAsync().ConfigureAwait(false);
            LoginState = LoginState.LoggedOut;

            await _loggedOutEvent.InvokeAsync().ConfigureAwait(false);
        }

        internal virtual Task OnLogoutAsync() => Task.CompletedTask;

        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _stateLock?.Dispose();
                _isDisposed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        internal virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!_isDisposed)
            {
                _stateLock?.Dispose();
                _isDisposed = true;
            }
        }

        public ValueTask DisposeAsync() => DisposeAsync(true);
    }
}
