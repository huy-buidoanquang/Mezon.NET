namespace Mezon.Net.Core
{
    /// <summary>
    ///     Default socket transport rate limits enforced client-side before sending over Abridged TCP / WebSocket.
    /// </summary>
    public static class MezonTransportLimits
    {
        public const int MaxRequestsPerSecond = 60;
        public const int MaxRequestsPerMinute = 500;
        public const int MaxConnectRequestsPerSecond = 2;
    }
}
