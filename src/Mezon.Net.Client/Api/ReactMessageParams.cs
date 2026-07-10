namespace Mezon.Net.Client
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
        public long SenderId { get; }
        public long? TopicId { get; }
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
            long? topicId = null,
            bool action = true)
        {
            ClanId = clanId;
            ChannelId = channelId;
            MessageId = messageId;
            EmojiId = emojiId;
            Emoji = emoji;
            Mode = mode;
            IsPublic = isPublic;
            SenderId = senderId;
            TopicId = topicId;
            Action = action;
        }
    }
}
