//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Mezon.Net.Abstractions;
//using Mezon.Net.Core;
//using Mezon.Net.Logging;

//namespace Mezon.Net.Api
//{
//    public abstract class BaseMezonClient : IMezonClient
//    {
//        internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new AsyncEvent<Func<LogMessage, Task>>();
//        public event Func<LogMessage, Task> Log { add { _logEvent.Add(value); } remove { _logEvent.Remove(value); } }

//        public event Func<Task> LoggedIn { add { _loggedInEvent.Add(value); } remove { _loggedInEvent.Remove(value); } }
//        private readonly AsyncEvent<Func<Task>> _loggedInEvent = new AsyncEvent<Func<Task>>();

//        public event Func<Task> LoggedOut { add { _loggedOutEvent.Add(value); } remove { _loggedOutEvent.Remove(value); } }
//        private readonly AsyncEvent<Func<Task>> _loggedOutEvent = new AsyncEvent<Func<Task>>();

//        internal readonly AsyncEvent<Func<string, string, double, Task>> _apiSentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();
//        /// <summary>
//        ///     Fired when a REST request is sent to the API. First parameter is the HTTP method,
//        ///     second is the endpoint, and third is the time taken to complete the request.
//        /// </summary>
//        public event Func<string, string, double, Task> ApiSentRequestEvent { add { _apiSentRequestEvent.Add(value); } remove { _apiSentRequestEvent.Remove(value); } }

//        internal readonly Logger _apiLogger;
//        private readonly SemaphoreSlim _stateLock;
//        private bool _isFirstLogin, _isDisposed;

//        protected readonly MezonApiClientConfiguration Configuration;

//        internal IMezonApiClient ApiClient { get; }

//        internal LogManager LogManager { get; }
//        /// <summary>
//        ///     Gets the login state of the client.
//        /// </summary>
//        public LoginState LoginState { get; private set; }

//        public TokenType TokenType => ApiClient.TokenType;

//        public virtual ConnectionState ConnectionState => ConnectionState.Disconnected;

//        internal BaseMezonClient(MezonApiClientConfiguration configuration, IMezonApiClient apiClient)
//        {
//            Configuration = configuration;
//            ApiClient = apiClient;

//            LogManager = new LogManager(configuration.LogLevel);
//            LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);
//            SessionManager.GetOrCreate(configuration, apiClient, LogManager);

//            _stateLock = new SemaphoreSlim(1, 1);
//            _apiLogger = LogManager.CreateLogger("MezonClient");
//            _isFirstLogin = configuration.DisplayInitialLog;

//            ApiClient.RequestQueue.RateLimitTriggered += async (id, info, endpoint) =>
//            {
//                if (info == null)
//                {
//                    await _apiLogger.DebugAsync($"Preemptive Rate limit triggered: {endpoint} {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
//                }
//                else
//                {
//                    await _apiLogger.WarningAsync($"Rate limit triggered: {endpoint} Remaining: {info.Value.RetryAfter}s {(id.IsHashBucket ? $"(Bucket: {id.BucketHash})" : "")}").ConfigureAwait(false);
//                }
//            };
//            ApiClient.ApiSentRequestEvent += async (method, endpoint, millis) => await _apiLogger.DebugAsync($"{method} {endpoint}: {millis} ms").ConfigureAwait(false);
//            ApiClient.ApiSentRequestEvent += (method, endpoint, millis) => _apiSentRequestEvent.InvokeAsync(method, endpoint, millis);
//        }

//        public virtual async Task<bool> LoginAsync(ISession session)
//        {
//            await _stateLock.WaitAsync().ConfigureAwait(false);
//            try
//            {
//                await SessionManager.Instance.LoginAsync(session).ConfigureAwait(false);
//                if (SessionManager.Instance.CurrentSession().IsExpired())
//                {
//                    return false;
//                }

//                await LoginInternalAsync(TokenType, SessionManager.Instance.CurrentSession().AuthToken).ConfigureAwait(false);
//                return true;
//            }
//            catch
//            {
//                return false;
//            }
//            finally
//            {
//                _stateLock.Release();
//            }
//        }

//        //public virtual async Task<bool> LoginAsync()
//        //{
//        //    await _stateLock.WaitAsync().ConfigureAwait(false);
//        //    try
//        //    {
//        //        await SessionManager.Instance.LoginAsync(Configuration., Configuration.ClientSecret).ConfigureAwait(false);
//        //        if (SessionManager.Instance.CurrentSession().IsExpired())
//        //        {
//        //            return false;
//        //        }

//        //        await LoginInternalAsync(TokenType, SessionManager.Instance.CurrentSession().AuthToken).ConfigureAwait(false);
//        //        return true;
//        //    }
//        //    catch
//        //    {
//        //        return false;
//        //    }
//        //    finally
//        //    {
//        //        _stateLock.Release();
//        //    }
//        //}

//        internal virtual async Task LoginInternalAsync(TokenType tokenType, string token)
//        {
//            if (_isFirstLogin)
//            {
//                _isFirstLogin = false;
//                await LogManager.WriteInitialLog().ConfigureAwait(false);
//            }

//            if (LoginState != LoginState.LoggedOut)
//            {
//                await LogoutInternalAsync().ConfigureAwait(false);
//            }

//            LoginState = LoginState.LoggingIn;

//            try
//            {
//                ApiClient.ConfigureApiBasePath(SessionManager.Instance.CurrentSession().ApiUrl ?? string.Empty);
//                await ApiClient.LoginAsync(tokenType, token).ConfigureAwait(false);
//                await OnLoginAsync(tokenType, token).ConfigureAwait(false);
//                LoginState = LoginState.LoggedIn;
//            }
//            catch
//            {
//                await LogoutInternalAsync().ConfigureAwait(false);
//                throw;
//            }

//            await _loggedInEvent.InvokeAsync().ConfigureAwait(false);
//        }

//        internal virtual Task OnLoginAsync(TokenType tokenType, string token) => Task.CompletedTask;

//        public async Task LogoutAsync()
//        {
//            await _stateLock.WaitAsync().ConfigureAwait(false);
//            try
//            {
//                await LogoutInternalAsync().ConfigureAwait(false);
//            }
//            finally
//            {
//                _stateLock.Release();
//            }
//        }

//        internal virtual async Task LogoutInternalAsync()
//        {
//            if (LoginState == LoginState.LoggedOut)
//            {
//                return;
//            }

//            LoginState = LoginState.LoggingOut;

//            await SessionManager.Instance.LogoutAsync().ConfigureAwait(false);
//            await ApiClient.LogoutAsync().ConfigureAwait(false);

//            await OnLogoutAsync().ConfigureAwait(false);
//            LoginState = LoginState.LoggedOut;

//            await _loggedOutEvent.InvokeAsync().ConfigureAwait(false);
//        }

//        internal virtual Task OnLogoutAsync() => Task.CompletedTask;

//        /// <inheritdoc />
//        Task IMezonClient.ConnectAsync()
//            => Task.CompletedTask;
//        /// <inheritdoc />
//        Task IMezonClient.DisconnectAsync()
//            => Task.CompletedTask;

//        internal virtual void Dispose(bool disposing)
//        {
//            if (!_isDisposed)
//            {
//                _stateLock?.Dispose();
//                _isDisposed = true;
//            }
//        }

//        /// <inheritdoc />
//        public void Dispose() => Dispose(true);

//        internal virtual async ValueTask DisposeAsync(bool disposing)
//        {
//            if (!_isDisposed)
//            {
//                _stateLock?.Dispose();
//                _isDisposed = true;
//            }
//        }

//        public ValueTask DisposeAsync() => DisposeAsync(true);
//    }
//}
