using System.IO;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Abstractions
{
    public interface IApiRequest : IRequest
    {
        IRestClient RestClient { get; }
        string Method { get; }
        string Endpoint { get; }
        TaskCompletionSource<Stream> Promise { get; }
        Task<HttpResponse> SendAsync();
    }
}
