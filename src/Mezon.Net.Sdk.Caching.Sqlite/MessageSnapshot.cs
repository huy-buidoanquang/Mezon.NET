namespace Mezon.Net.Sdk.Caching.Sqlite
{
    /// <summary>
    ///     Serializable message payload stored in SQLite. JSON fields mirror the TS cache shape
    ///     (<c>mentions</c>, <c>attachments</c>, <c>reactions</c>, <c>msg_references</c>).
    /// </summary>
    public sealed class MessageSnapshot
    {
        public long MessageId { get; set; }

        public long ChannelId { get; set; }

        public long ClanId { get; set; }

        public long SenderId { get; set; }

        public string Content { get; set; } = string.Empty;

        public string MentionsJson { get; set; } = "[]";

        public string AttachmentsJson { get; set; } = "[]";

        public string ReactionsJson { get; set; } = "[]";

        public string ReferencesJson { get; set; } = "[]";

        public long? TopicId { get; set; }

        public long? CreateTimeSeconds { get; set; }
    }
}
