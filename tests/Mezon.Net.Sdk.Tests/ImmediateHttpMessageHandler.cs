using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Tests;

internal sealed class ImmediateHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });
}
