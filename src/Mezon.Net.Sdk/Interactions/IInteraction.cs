namespace Mezon.Net.Sdk.Interactions
{
    /// <remarks>
    /// <see cref="MessageId"/>, <see cref="ChannelId"/> and <see cref="SenderId"/> are supplied by the clicking
    /// client and are not verified by the server. Whether <see cref="UserId"/> is verified depends on
    /// <see cref="IInteractionActor.ActorTrust"/>.
    /// </remarks>
    public interface IInteraction
    {
        InteractionKind Kind { get; }
        long MessageId { get; }
        long ChannelId { get; }
        long UserId { get; }
        long SenderId { get; }
        string CustomId { get; }
    }
}
