namespace Mezon.Net.Sdk.Interactions
{
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
