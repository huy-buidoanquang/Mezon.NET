using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api
{
    public class JsonApiRequest : ApiRequest
    {
        public string Json { get; }

        public JsonApiRequest(IHttpClient client, string method, string endpoint, string json, RequestOptions options)
            : base(client, method, endpoint, options)
        {
            Json = json;
        }

        public override Task<HttpResponse> SendAsync()
            => ApiClient.SendAsync(Method, Endpoint, Json, Options.CancelToken, Options.HeaderOnly, Options.RequestHeaders);
    }
}
