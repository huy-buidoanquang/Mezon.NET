namespace Mezon.Net.Sdk.Interactions
{
    public static class MezonClientInteractionExtensions
    {
        public static MezonClient UseInteractions(this MezonClient client, InteractionRouter router)
            => router.Attach(client);
    }
}
