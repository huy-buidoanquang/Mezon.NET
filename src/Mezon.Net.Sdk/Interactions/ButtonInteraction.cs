using System;

namespace Mezon.Net.Sdk.Interactions
{
    public sealed class ButtonInteraction : IInteraction, IInteractionActor
    {
        public ButtonInteraction(
            long messageId,
            long channelId,
            string buttonId,
            long userId,
            long senderId,
            string? extraData = null,
            InteractionActorTrust actorTrust = InteractionActorTrust.Unknown)
        {
            MessageId = messageId;
            ChannelId = channelId;
            CustomId = buttonId ?? string.Empty;
            UserId = userId;
            SenderId = senderId;
            ExtraData = extraData ?? string.Empty;
            ActorTrust = actorTrust;
        }

        public InteractionKind Kind => InteractionKind.Button;
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public string ExtraData { get; }
        public InteractionActorTrust ActorTrust { get; }
    }
}
