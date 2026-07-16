namespace Mezon.Net.Core
{
    /// <summary>
    ///     Well-known transport rate-limit bucket identifiers used by the client-side limiter.
    /// </summary>
    public static class RateLimitBuckets
    {
        /// <summary>
        ///     Global per-second socket transport bucket.
        /// </summary>
        public const string TransportPerSecond = "transport/per-second";

        /// <summary>
        ///     Global per-minute socket transport bucket.
        /// </summary>
        public const string TransportPerMinute = "transport/per-minute";

        /// <summary>
        ///     Connect-phase per-second socket transport bucket.
        /// </summary>
        public const string TransportConnect = "transport/connect";
    }
}
