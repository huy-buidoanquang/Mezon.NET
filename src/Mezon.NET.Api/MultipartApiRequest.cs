using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api
{
    public class MultipartApiRequest : ApiRequest
    {
        public IReadOnlyDictionary<string, object> MultipartParams { get; }

        public MultipartApiRequest(IHttpClient apiClient, string method, string endpoint, IReadOnlyDictionary<string, object> multipartParams, RequestOptions options)
            : base(apiClient, method, endpoint, options)
        {
            MultipartParams = multipartParams;
        }

        public override Task<HttpResponse> SendAsync()
            => ApiClient.SendAsync(Method, Endpoint, MultipartParams, Options.CancelToken, Options.HeaderOnly);
    }
}
