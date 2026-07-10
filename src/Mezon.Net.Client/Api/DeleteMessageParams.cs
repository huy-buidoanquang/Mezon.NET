namespace Mezon.Net.Client
{
    public readonly struct DeleteMessageParams
    {
        public long ClanId { get; }
        public long ChannelId { get; }
        public long MessageId { get; }
        public int Mode { get; }
        public bool IsPublic { get; }
        public long? TopicId { get; }
        public bool HasAttachment { get; }

        public DeleteMessageParams(
            long clanId,
            long channelId,
            long messageId,
            int mode,
            bool isPublic,
            long? topicId = null,
            bool hasAttachment = false)
        {
            ClanId = clanId;
            ChannelId = channelId;
            MessageId = messageId;
            Mode = mode;
            IsPublic = isPublic;
            TopicId = topicId;
            HasAttachment = hasAttachment;
        }
    }
}
