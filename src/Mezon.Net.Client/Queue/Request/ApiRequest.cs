using System;
using System.IO;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    /// <summary>
    ///     Represents a REST API request executed through <see cref="IRestClient"/>.
    /// </summary>
    public class ApiRequest : IApiRequest
    {
        /// <summary>
        ///     Gets the REST client used to send this request.
        /// </summary>
        public IRestClient RestClient { get; }

        /// <summary>
        ///     Gets the HTTP method (for example <c>GET</c> or <c>POST</c>).
        /// </summary>
        public string Method { get; }

        /// <summary>
        ///     Gets the relative endpoint path.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        ///     Gets the absolute UTC time when this request should time out, if configured.
        /// </summary>
        public DateTimeOffset? TimeoutAt { get; }

        /// <summary>
        ///     Gets the completion source that will receive the response stream.
        /// </summary>
        public TaskCompletionSource<Stream> Promise { get; }

        /// <summary>
        ///     Gets the per-request options applied to this call.
        /// </summary>
        public RequestOptions Options { get; }

        /// <summary>
        ///     Initializes a new <see cref="ApiRequest"/>.
        /// </summary>
        /// <param name="restClient">REST client used to send the request.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="endpoint">Relative endpoint path.</param>
        /// <param name="options">Per-request options.</param>
        public ApiRequest(IRestClient restClient, string method, string endpoint, RequestOptions options)
        {
            Check.NotNull(options, nameof(options));

            RestClient = restClient;
            Method = method;
            Endpoint = endpoint;
            Options = options;
            TimeoutAt = options.ApiSendTimeout.HasValue ? DateTimeOffset.UtcNow.AddMilliseconds(options.ApiSendTimeout.Value) : (DateTimeOffset?)null;
            Promise = new TaskCompletionSource<Stream>();
        }

        /// <summary>
        ///     Sends this request and returns the HTTP response.
        /// </summary>
        /// <returns>A task that resolves to the HTTP response.</returns>
        public virtual Task<HttpResponse> SendAsync()
            => RestClient.SendAsync(
                Method,
                Endpoint,
                Options.CancelToken,
                Options.HeaderOnly,
                Options.HasRequestHeaders ? Options.RequestHeaders : null);
    }
}
