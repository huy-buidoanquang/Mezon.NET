using System.Collections.Generic;

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

    public sealed class ButtonInteraction : IInteraction
    {
        public ButtonInteraction(
            long messageId,
            long channelId,
            string buttonId,
            long userId,
            long senderId,
            string? extraData = null)
        {
            MessageId = messageId;
            ChannelId = channelId;
            CustomId = buttonId ?? string.Empty;
            UserId = userId;
            SenderId = senderId;
            ExtraData = extraData ?? string.Empty;
        }

        public InteractionKind Kind => InteractionKind.Button;
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public string ExtraData { get; }
    }

    public sealed class SelectInteraction : IInteraction
    {
        public SelectInteraction(
            long messageId,
            long channelId,
            string selectboxId,
            long userId,
            long senderId,
            IReadOnlyList<string> values)
        {
            MessageId = messageId;
            ChannelId = channelId;
            CustomId = selectboxId ?? string.Empty;
            UserId = userId;
            SenderId = senderId;
            Values = values ?? System.Array.Empty<string>();
        }

        public InteractionKind Kind => InteractionKind.Select;
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public IReadOnlyList<string> Values { get; }
    }

    public sealed class UnknownInteraction : IInteraction
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
        }

        public InteractionKind Kind { get; }
        public long MessageId { get; }
        public long ChannelId { get; }
        public long UserId { get; }
        public long SenderId { get; }
        public string CustomId { get; }
        public IInteraction Source { get; }
    }
}
