using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Grpc.Core;
using Mezon.Net.Abstractions;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    /// <summary>
    ///     Represents a REST-Based ratelimit info.
    /// </summary>
    public struct RateLimitInfo : IRateLimitInfo
    {
        /// <inheritdoc/>
        public bool IsGlobal { get; }

        /// <inheritdoc/>
        public int? Limit { get; }

        /// <inheritdoc/>
        public int? Remaining { get; }

        /// <inheritdoc/>
        public int? RetryAfter { get; }

        /// <inheritdoc/>
        public DateTimeOffset? Reset { get; }

        /// <inheritdoc/>
        public TimeSpan? ResetAfter { get; private set; }

        /// <inheritdoc/>
        public string Bucket { get; }

        /// <inheritdoc/>
        public TimeSpan? Lag { get; }

        /// <inheritdoc/>
        public string Endpoint { get; }

        internal RateLimitInfo(Metadata headers, string endpoint)
        {
            Endpoint = endpoint;
            IsGlobal = false;
            Limit = 50;
            Remaining = null;
            Reset = null;
            RetryAfter = null;
            ResetAfter = null;
            Bucket = string.Empty;
            Lag = null;
        }

        internal RateLimitInfo(Dictionary<string, string> headers, string endpoint)
        {
            Endpoint = endpoint;
            string? temp = string.Empty;
            IsGlobal = headers.TryGetValue("X-RateLimit-Global", out temp) &&
                       bool.TryParse(temp, out var isGlobal) && isGlobal;
            Limit = headers.TryGetValue("X-RateLimit-Limit", out temp) &&
                int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ? limit : (int?)null;
            Remaining = headers.TryGetValue("X-RateLimit-Remaining", out temp) &&
                int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var remaining) ? remaining : (int?)null;
            Reset = headers.TryGetValue("X-RateLimit-Reset", out temp) &&
                double.TryParse(temp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var reset) ? DateTimeOffset.FromUnixTimeMilliseconds((long)(reset * 1000)) : (DateTimeOffset?)null;
            RetryAfter = headers.TryGetValue("Retry-After", out temp) &&
                int.TryParse(temp, NumberStyles.None, CultureInfo.InvariantCulture, out var retryAfter) ? retryAfter : (int?)null;
            ResetAfter = headers.TryGetValue("X-RateLimit-Reset-After", out temp) &&
                double.TryParse(temp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var resetAfter) ? TimeSpan.FromSeconds(resetAfter) : (TimeSpan?)null;
            Bucket = headers.TryGetValue("X-RateLimit-Bucket", out temp) ? temp : string.Empty;
            Lag = headers.TryGetValue("Date", out temp) &&
                DateTimeOffset.TryParse(temp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? DateTimeOffset.UtcNow - date : (TimeSpan?)null;
        }

        internal RatelimitResponse? ReadRatelimitPayload(Stream response)
        {
            if (response != null && response.Length != 0)
            {
                using (TextReader text = new StreamReader(response))
                using (JsonReader reader = new JsonTextReader(text))
                {
                    return new JsonSerializer().Deserialize<RatelimitResponse>(reader);
                }
            }

            return null;
        }
    }
}
