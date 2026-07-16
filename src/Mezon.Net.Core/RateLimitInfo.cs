using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Mutable rate-limit snapshot used when invoking <see cref="RequestOptions.RatelimitCallback"/>.
    /// </summary>
    internal sealed class RateLimitInfo : IRateLimitInfo
    {
        /// <inheritdoc />
        public bool IsGlobal { get; set; }

        /// <inheritdoc />
        public int Limit { get; set; }

        /// <inheritdoc />
        public int Remaining { get; set; }

        /// <inheritdoc />
        public TimeSpan ResetAfter { get; set; }

        /// <inheritdoc />
        public string Bucket { get; set; } = string.Empty;
    }
}
