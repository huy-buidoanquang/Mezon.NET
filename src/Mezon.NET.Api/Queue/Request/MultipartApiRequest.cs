using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Abstractions;

namespace Mezon.NET.Queue
{
    public class MultipartApiRequest : ApiRequest
    {
        public IReadOnlyDictionary<string, object> MultipartParams { get; }

        public MultipartApiRequest(IRestClient restClient, string method, string endpoint, IReadOnlyDictionary<string, object> multipartParams, RequestOptions options)
            : base(restClient, method, endpoint, options)
        {
            MultipartParams = multipartParams;
        }

        public override Task<HttpResponse> SendAsync()
            => RestClient.SendAsync(Method, Endpoint, MultipartParams, Options.CancelToken, Options.HeaderOnly);
    }
}
