using System.IO;
using System.Threading.Tasks;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api.Abstractions
{
    public interface IApiRequest : IRequest
    {
        IHttpClient ApiClient { get; }
        string Method { get; }
        string Endpoint { get; }
        TaskCompletionSource<Stream> Promise { get; }
        Task<HttpResponse> SendAsync();
    }
}
