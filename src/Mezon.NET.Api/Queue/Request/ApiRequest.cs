using System;
using System.IO;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;

namespace Mezon.NET.Queue
{
    public class ApiRequest : IApiRequest
    {
        public IRestClient RestClient { get; }
        public string Method { get; }
        public string Endpoint { get; }
        public DateTimeOffset? TimeoutAt { get; }
        public TaskCompletionSource<Stream> Promise { get; }
        public RequestOptions Options { get; }

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

        public virtual Task<HttpResponse> SendAsync()
            => RestClient.SendAsync(Method, Endpoint, Options.CancelToken, Options.HeaderOnly, Options.RequestHeaders);
    }
}
