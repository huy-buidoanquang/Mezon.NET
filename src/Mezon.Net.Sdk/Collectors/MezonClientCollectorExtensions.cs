namespace Mezon.Net.Sdk.Collectors
{
    public static class MezonClientCollectorExtensions
    {
        public static MezonClient UseCollectors(this MezonClient client, CollectorService collectors)
            => collectors.Attach(client);
    }
}
