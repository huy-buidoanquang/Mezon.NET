using Mezon.NET.Abstractions;

namespace Mezon.NET.Abstractions
{
    public interface IApiClientProvider
    {
        IMezonApiClient MezonApiClient { get; }
    }
}
