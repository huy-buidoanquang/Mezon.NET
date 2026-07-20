namespace Mezon.Net.Sdk.Commands
{
    public static class MezonClientCommandExtensions
    {
        public static MezonClient UseCommands(this MezonClient client, CommandService service)
            => service.Attach(client);
    }
}
