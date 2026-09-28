namespace Mezon.Net.Sdk.Interactions
{
    /// <summary>
    /// Exposes actor provenance without changing the IInteraction contract.
    /// </summary>
    public interface IInteractionActor
    {
        InteractionActorTrust ActorTrust { get; }
    }
}
