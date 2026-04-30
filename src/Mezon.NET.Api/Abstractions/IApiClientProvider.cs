namespace Mezon.Net.Abstractions
{
    public interface IApiClientProvider
    {
        IMezonClient MezonApiClient { get; }
    }
}
