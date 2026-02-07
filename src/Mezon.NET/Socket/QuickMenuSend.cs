using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a quick menu event, containing details about the menu and the associated message.
    /// </summary>
    public class QuickMenuSend : SocketSendBase
    {
        [JsonPropertyName("quick_menu_event")]
        public QuickMenuSendDetails QuickMenuSendDetails { get; set; }
    }

    /// <summary>
    /// Contains the top-level details for a quick menu event.
    /// </summary>
    public class QuickMenuSendDetails
    {
        [JsonPropertyName("menu_name")]
        public string MenuName { get; set; }

        [JsonPropertyName("message")]
        public QuickMenuMessagePayload Message { get; set; }
    }

    /// <summary>
    /// Contains the detailed content of the message associated with the quick menu event.
    /// </summary>
    public class QuickMenuMessagePayload
    {
        /// <summary>
        /// The ID of the clan the channel belongs to.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The server-assigned channel ID.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// The channel label.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// The content payload, which can be any serializable object.
        /// </summary>
        [JsonPropertyName("content")]
        public object Content { get; set; }

        [JsonPropertyName("mentions")]
        public List<MessageMention>? Mentions { get; set; }

        [JsonPropertyName("attachments")]
        public List<MessageAttachment>? Attachments { get; set; }

        [JsonPropertyName("anonymous_message")]
        public bool? AnonymousMessage { get; set; }

        [JsonPropertyName("mention_everyone")]
        public bool? MentionEveryone { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }
    }
}
