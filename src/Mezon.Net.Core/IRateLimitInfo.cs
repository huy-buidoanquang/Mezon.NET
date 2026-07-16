using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Describes a client-side or server-reported rate limit that caused a request to be delayed.
    /// </summary>
    public interface IRateLimitInfo
    {
        /// <summary>
        ///     Gets whether this limit applies globally to the transport rather than a single endpoint.
        /// </summary>
        bool IsGlobal { get; }

        /// <summary>
        ///     Gets the maximum number of requests allowed in the current window.
        /// </summary>
        int Limit { get; }

        /// <summary>
        ///     Gets the number of requests remaining in the current window before the limit is hit.
        /// </summary>
        int Remaining { get; }

        /// <summary>
        ///     Gets how long to wait before the bucket will accept another request.
        /// </summary>
        TimeSpan ResetAfter { get; }

        /// <summary>
        ///     Gets the bucket identifier (for example <see cref="RateLimitBuckets.TransportPerSecond"/>).
        /// </summary>
        string Bucket { get; }
    }
}
