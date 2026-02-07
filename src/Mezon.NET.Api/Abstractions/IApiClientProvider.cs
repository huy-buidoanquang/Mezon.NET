namespace Mezon.NET.Api.Abstractions
{
    public interface IApiClientProvider
    {
        IMezonApiClient MezonApiClient { get; }
    }
}
