using System.IO;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;

namespace Mezon.NET.Abstractions
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
