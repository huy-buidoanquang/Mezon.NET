namespace Mezon.Net.Models
{
    public readonly struct ReactMessageParams
    {
        public long ClanId { get; }
        public long ChannelId { get; }
        public long MessageId { get; }
        public long EmojiId { get; }
        public string Emoji { get; }
        public int Mode { get; }
        public bool IsPublic { get; }
        public long? TopicId { get; }
        public long SenderId { get; }
        public bool Action { get; }

        public ReactMessageParams(
            long clanId,
            long channelId,
            long messageId,
            long emojiId,
            string emoji,
            int mode,
            bool isPublic,
            long senderId,
            bool action,
            long? topicId = null)
        {
            ClanId = clanId;
            ChannelId = channelId;
            MessageId = messageId;
            EmojiId = emojiId;
            Emoji = emoji;
            Mode = mode;
            IsPublic = isPublic;
            SenderId = senderId;
            Action = action;
            TopicId = topicId;
        }
    }
}
