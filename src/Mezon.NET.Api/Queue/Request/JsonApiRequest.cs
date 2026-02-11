using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Abstractions;

namespace Mezon.NET.Queue
{
    public class JsonApiRequest : ApiRequest
    {
        public string Json { get; }

        public JsonApiRequest(IRestClient restClient, string method, string endpoint, string json, RequestOptions options)
            : base(restClient, method, endpoint, options)
        {
            Json = json;
        }

        public override Task<HttpResponse> SendAsync()
            => RestClient.SendAsync(Method, Endpoint, Json, Options.CancelToken, Options.HeaderOnly, Options.RequestHeaders);
    }
}
