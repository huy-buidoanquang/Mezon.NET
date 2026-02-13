namespace Mezon.NET.Abstractions
{
    public interface IApiClientProvider
    {
        IMezonClient MezonApiClient { get; }
    }
}
