namespace Mezon.Net.Models
{
    public readonly struct UpdateMessageParams
    {
        public long ClanId { get; }
        public long ChannelId { get; }
        public long MessageId { get; }
        public string Content { get; }
        public int Mode { get; }
        public bool IsPublic { get; }
        public long? TopicId { get; }
        public bool HideEdited { get; }

        public UpdateMessageParams(
            long clanId,
            long channelId,
            long messageId,
            string content,
            int mode,
            bool isPublic,
            long? topicId = null,
            bool hideEdited = false)
        {
            ClanId = clanId;
            ChannelId = channelId;
            MessageId = messageId;
            Content = content;
            Mode = mode;
            IsPublic = isPublic;
            TopicId = topicId;
            HideEdited = hideEdited;
        }
    }
}
