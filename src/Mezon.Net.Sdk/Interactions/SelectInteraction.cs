using System;
using System.Collections.Generic;

namespace Mezon.Net.Sdk.Interactions
{
    public sealed class SelectInteraction : IInteraction, IInteractionActor
    {
        public SelectInteraction(
            long messageId,
            long channelId,
            string selectboxId,
            long userId,
            long senderId,
            IReadOnlyList<string> values,
            InteractionActorTrust actorTrust = InteractionActorTrust.Unknown)
        {
            MessageId = messageId;
            ChannelId = channelId;
            CustomId = selectboxId ?? string.Empty;
            UserId = userId;
            SenderId = senderId;
            Values = values ?? Array.Empty<string>();
            ActorTrust = actorTrust;
        }

        public InteractionKind Kind => InteractionKind.Select;
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public IReadOnlyList<string> Values { get; }
        public InteractionActorTrust ActorTrust { get; }
    }
}
