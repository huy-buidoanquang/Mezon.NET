namespace Mezon.Net.Sdk.Interactions
{
    public sealed class UnknownInteraction : IInteraction, IInteractionActor
    {
        public UnknownInteraction(IInteraction source)
        {
            Source = source;
            MessageId = source.MessageId;
            ChannelId = source.ChannelId;
            UserId = source.UserId;
            SenderId = source.SenderId;
            CustomId = source.CustomId;
            Kind = source.Kind;
            ActorTrust = source is IInteractionActor actor
                ? actor.ActorTrust
                : InteractionActorTrust.Unknown;
        }

        public InteractionKind Kind { get; }
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public InteractionActorTrust ActorTrust { get; }
        public IInteraction Source { get; }
    }
}
