namespace Mezon.Net.Sdk.Interactions
{
    /// <summary>
    /// Describes how the actor id on an interaction was established.
    /// </summary>
    public enum InteractionActorTrust
    {
        Unknown = 0,
        ClientSupplied = 1,
        ServerAuthenticated = 2,
    }
}
