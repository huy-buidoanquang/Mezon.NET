using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Api.ApiRequests;
using Microsoft.Extensions.Logging;

namespace Mezon.NET.Managers
{
    public class SessionManager : ISessionManager, IDisposable
    {
        private readonly ILogger<ISessionManager> _logger;
        private const int RefreshTimeBufferInSeconds = 30;
        private readonly SemaphoreSlim _sessionRefreshLock = new SemaphoreSlim(1, 1);
        private Timer? _sessionCheckTimer;
        protected volatile Session _session;

        protected IMezonApiClient MezonApi { get; }
        protected bool AutoRefreshSession { get; private set; }

        public SessionManager(IMezonApiClient mezonApi, ILogger<ISessionManager> logger)
        {
            MezonApi = mezonApi;
            _logger = logger;
            _session = Session.NullSession();
            AutoRefreshSession = true;
        }

        public async Task<bool> AuthenticateAsync(string token, bool autoRefreshSession = true)
        {
            StopSessionTimer();
            AutoRefreshSession = autoRefreshSession;
            var apiSession = await MezonApi.AuthenticateAppAsync(
                basicAuthUsername: token,
                basicAuthPassword: string.Empty,
                body: new AppAuthenticationRequest(new AppAccountRequest { Token = token })
            );

            if (apiSession != null && !string.IsNullOrEmpty(apiSession.Token))
            {
                _session = new Session(apiSession);
                StartSessionTimer();
                return true;
            }

            _session = Session.NullSession();
            return false;
        }

        public Task<bool> LogoutAsync()
        {
            _session = Session.NullSession();
            return Task.FromResult(true);
        }

        public Session CurrentSession() => _session;

        private async Task RefreshSessionIfNeededAsync(CancellationToken cancellationToken)
        {
            if (!AutoRefreshSession || _session == null || !_session.IsExpiredSoon(RefreshTimeBufferInSeconds))
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

                var request = new SessionRefreshRequest { RefreshToken = _session.RefreshToken };
                var newSession = await MezonApi.RefreshSessionAsync("", "", request, cancellationToken);

                if (newSession != null && !string.IsNullOrEmpty(newSession.Token))
                {
                    _session = new Session(newSession);
                }
                else
                {
                    _session = Session.NullSession();
                    StopSessionTimer();
                    //throw new SessionRefreshFailedException("Failed to refresh session.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while refreshing the session.");
                _session = Session.NullSession();
            }
            finally
            {
                _sessionRefreshLock.Release();
            }
        }

        private void StartSessionTimer()
        {
            if (!AutoRefreshSession)
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

        public void Dispose()
        {
            StopSessionTimer();
        }
    }
}
