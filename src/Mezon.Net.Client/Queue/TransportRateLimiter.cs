using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    /// <summary>
    ///     Enforces Mezon socket transport limits: per-second, per-minute, and connect-phase caps.
    /// </summary>
    internal sealed class TransportRateLimiter
    {
        private readonly SlidingWindowRateLimiter _perSecond;
        private readonly SlidingWindowRateLimiter _perMinute;
        private readonly SlidingWindowRateLimiter _connectPerSecond;
        private int _connectPhase;

        public TransportRateLimiter(
            int maxRequestsPerSecond = MezonTransportLimits.MaxRequestsPerSecond,
            int maxRequestsPerMinute = MezonTransportLimits.MaxRequestsPerMinute,
            int maxConnectRequestsPerSecond = MezonTransportLimits.MaxConnectRequestsPerSecond)
        {
            _perSecond = new SlidingWindowRateLimiter(maxRequestsPerSecond, 1);
            _perMinute = new SlidingWindowRateLimiter(maxRequestsPerMinute, 60);
            _connectPerSecond = new SlidingWindowRateLimiter(maxConnectRequestsPerSecond, 1);
        }

        public void BeginConnectPhase() => Volatile.Write(ref _connectPhase, 1);

        public void EndConnectPhase() => Volatile.Write(ref _connectPhase, 0);

        public async ValueTask EnterAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _connectPhase) != 0)
            {
                await _connectPerSecond.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await _perSecond.WaitAsync(cancellationToken).ConfigureAwait(false);
            await _perMinute.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Reset()
        {
            _perSecond.Reset();
            _perMinute.Reset();
            _connectPerSecond.Reset();
            EndConnectPhase();
        }
    }
}
