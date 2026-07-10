namespace Mezon.Net.Client
{
    /// <summary>
    /// Low-allocation input for replying to a channel message over socket API.
    /// </summary>
    public readonly struct ReplyMessageParams
    {
        public long ClanId { get; }
        public long ChannelId { get; }
        public string Content { get; }
        public int ChannelType { get; }
        public bool IsPublic { get; }
        public long ReplyToMessageId { get; }
        public long ReplyToSenderId { get; }
        public string? ReplyToSenderUsername { get; }
        public string? ReplyToSenderAvatar { get; }
        public string? ReplyToContent { get; }
        public long? TopicId { get; }
        public int Code { get; }
        public bool MentionEveryone { get; }
        public bool AnonymousMessage { get; }

        public ReplyMessageParams(
            long clanId,
            long channelId,
            string content,
            int channelType,
            bool isPublic,
            long replyToMessageId,
            long replyToSenderId,
            string? replyToSenderUsername = null,
            string? replyToSenderAvatar = null,
            string? replyToContent = null,
            long? topicId = null,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false)
        {
            ClanId = clanId;
            ChannelId = channelId;
            Content = content;
            ChannelType = channelType;
            IsPublic = isPublic;
            ReplyToMessageId = replyToMessageId;
            ReplyToSenderId = replyToSenderId;
            ReplyToSenderUsername = replyToSenderUsername;
            ReplyToSenderAvatar = replyToSenderAvatar;
            ReplyToContent = replyToContent;
            TopicId = topicId;
            Code = code;
            MentionEveryone = mentionEveryone;
            AnonymousMessage = anonymousMessage;
        }
    }
}
