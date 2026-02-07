using System;
using System.IO;
using System.Threading.Tasks;
using Mezon.NET.Api.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api
{
    public class ApiRequest : IApiRequest
    {
        public IHttpClient ApiClient { get; }
        public string Method { get; }
        public string Endpoint { get; }
        public DateTimeOffset? TimeoutAt { get; }
        public TaskCompletionSource<Stream> Promise { get; }
        public RequestOptions Options { get; }

        public ApiRequest(IHttpClient apiClient, string method, string endpoint, RequestOptions options)
        {
            Check.NotNull(options, nameof(options));

            ApiClient = apiClient;
            Method = method;
            Endpoint = endpoint;
            Options = options;
            TimeoutAt = options.Timeout.HasValue ? DateTimeOffset.UtcNow.AddMilliseconds(options.Timeout.Value) : (DateTimeOffset?)null;
            Promise = new TaskCompletionSource<Stream>();
        }

        public virtual Task<HttpResponse> SendAsync()
            => ApiClient.SendAsync(Method, Endpoint, Options.CancelToken, Options.HeaderOnly, Options.RequestHeaders);
    }
}
