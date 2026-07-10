namespace Mezon.Net.Models
{
    /// <summary>
    /// Low-allocation input for sending a channel message over socket API.
    /// </summary>
    public readonly struct SendChannelMessageParams
    {
        public long ClanId { get; }
        public long ChannelId { get; }
        public string Content { get; }
        public long? TopicId { get; }
        public bool IsPublic { get; }
        public int Mode { get; }
        public int Code { get; }
        public bool MentionEveryone { get; }
        public bool AnonymousMessage { get; }

        public SendChannelMessageParams(
            long clanId,
            long channelId,
            string content,
            long? topicId = null,
            bool isPublic = false,
            int mode = 0,
            int code = 0,
            bool mentionEveryone = false,
            bool anonymousMessage = false)
        {
            ClanId = clanId;
            ChannelId = channelId;
            Content = content;
            TopicId = topicId;
            IsPublic = isPublic;
            Mode = mode;
            Code = code;
            MentionEveryone = mentionEveryone;
            AnonymousMessage = anonymousMessage;
        }
    }
}
