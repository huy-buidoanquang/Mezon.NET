using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Represents options that should be used when sending a request.
    /// </summary>
    public class RequestOptions
    {
        private IDictionary<string, IEnumerable<string>>? _requestHeaders;

        /// <summary>
        ///     Creates a new <see cref="RequestOptions"/> instance with default settings.
        /// </summary>
        /// <remarks>
        ///     Each access returns a new instance. Mutating the returned object does not affect later accesses.
        /// </remarks>
        public static RequestOptions Default => new RequestOptions();

        /// <summary>
        ///     Gets or sets the maximum time to wait for this request to complete.
        /// </summary>
        /// <remarks>
        ///     Gets or sets the max time, in milliseconds, to wait for this request to complete. If
        ///     <see langword="null"/>, a request will not time out. If a rate limit has been triggered for this request's bucket
        ///     and will not be unpaused in time, this request will fail immediately.
        /// </remarks>
        /// <returns>
        ///     A <see cref="int"/> in milliseconds for when the request times out.
        /// </returns>
        public int? ApiSendTimeout { get; set; }

        /// <summary>
        ///     Gets or sets the maximum time, in milliseconds, to wait for a socket send to complete.
        /// </summary>
        public int? SocketSendTimeout { get; set; }

        /// <summary>
        ///     Gets or sets the cancellation token for this request.
        /// </summary>
        /// <returns>
        ///     A <see cref="CancellationToken"/> for this request.
        /// </returns>
        public CancellationToken CancelToken { get; set; } = CancellationToken.None;

        /// <summary>
        ///     Gets or sets whether only response headers should be retrieved.
        /// </summary>
        public bool HeaderOnly { get; internal set; }

        /// <summary>
        ///     Gets or sets the reason for this action in the guild's audit log.
        /// </summary>
        /// <remarks>
        ///     Gets or sets the reason that will be written to the guild's audit log if applicable. This may not apply
        ///     to all actions.
        /// </remarks>
        public string? AuditLogReason { get; set; }

        /// <summary>
        ///     Gets or sets a callback invoked when this request is delayed by a rate limiter.
        /// </summary>
        /// <remarks>
        ///     When <see langword="null"/>, <see cref="MezonOptions.DefaultRatelimitCallback"/> is used if configured
        ///     on the owning client options.
        /// </remarks>
        public Func<IRateLimitInfo, Task>? RatelimitCallback { get; set; }

        /// <summary>
        ///     Gets or sets whether this request should skip the client-side transport rate limiter.
        /// </summary>
        /// <remarks>
        ///     Prefer <see cref="IRateLimitInfo.SendBypassMessageAsync"/> for rate-limit warnings. Setting this on
        ///     ordinary traffic defeats client throttling and can trigger server-side limits.
        /// </remarks>
        public bool BypassRateLimiter { get; set; }

        internal bool IgnoreState { get; set; }

        /// <summary>
        ///     Gets custom HTTP headers to include with the request.
        /// </summary>
        /// <remarks>
        ///     The dictionary is created lazily on first access to avoid allocations for requests that do not set headers.
        /// </remarks>
        public IDictionary<string, IEnumerable<string>> RequestHeaders =>
            _requestHeaders ??= new Dictionary<string, IEnumerable<string>>();

        /// <summary>
        ///     Returns <see langword="true"/> when custom request headers have been materialized.
        /// </summary>
        internal bool HasRequestHeaders => _requestHeaders != null && _requestHeaders.Count > 0;

        /// <summary>
        ///     Creates a new instance when <paramref name="options"/> is <see langword="null"/>; otherwise returns a clone.
        /// </summary>
        /// <param name="options">Existing options to clone, or <see langword="null"/> to create defaults.</param>
        /// <returns>A mutable <see cref="RequestOptions"/> instance safe for per-request mutation.</returns>
        internal static RequestOptions CreateOrClone(RequestOptions? options = null)
        {
            if (options == null)
            {
                return new RequestOptions();
            }

            return options.Clone();
        }

        /// <summary>
        ///     Initializes a new <see cref="RequestOptions"/> class with the default request timeout set in
        ///     <see cref="MezonOptions"/>.
        /// </summary>
        public RequestOptions()
        {
            ApiSendTimeout = MezonOptions.DefaultApiTimeoutInMilliseconds;
        }

        /// <summary>
        ///     Creates a copy of this instance. Request headers are deep-copied when present.
        /// </summary>
        /// <returns>A new <see cref="RequestOptions"/> with the same settings.</returns>
        public RequestOptions Clone()
        {
            var clone = (RequestOptions)MemberwiseClone();
            if (_requestHeaders != null)
            {
                clone._requestHeaders = new Dictionary<string, IEnumerable<string>>(_requestHeaders);
            }

            return clone;
        }

        /// <summary>
        ///     Applies <paramref name="defaultCallback"/> when this instance has no callback set.
        /// </summary>
        /// <param name="defaultCallback">The client-wide default rate-limit callback.</param>
        internal void ApplyDefaultRatelimitCallback(Func<IRateLimitInfo, Task>? defaultCallback)
        {
            if (RatelimitCallback == null && defaultCallback != null)
            {
                RatelimitCallback = defaultCallback;
            }
        }
    }
}
