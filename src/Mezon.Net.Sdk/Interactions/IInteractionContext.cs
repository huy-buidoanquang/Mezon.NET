using System;
using Mezon.Net.Sdk.Interactions;

namespace Mezon.Net.Sdk.Interactions
{
    public interface IInteractionContext
    {
        MezonClient Client { get; }
        long ClanId { get; }
        long ChannelId { get; }
        long UserId { get; }
        long MessageId { get; }
    }

    internal sealed class InteractionContext : IInteractionContext
    {
        public InteractionContext(MezonClient client, long clanId, long channelId, long userId, long messageId)
        {
            Client = client;
            ClanId = clanId;
            ChannelId = channelId;
            UserId = userId;
            MessageId = messageId;
        }

        public MezonClient Client { get; }
        public long ClanId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long MessageId { get; }
    }
}
