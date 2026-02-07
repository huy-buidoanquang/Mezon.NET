using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents a detailed description of a channel.
    /// </summary>
    public class ChannelDescriptionResponse
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("age_restricted")]
        public int? AgeRestricted { get; set; }

        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("channel_avatar")]
        public List<string>? ChannelAvatar { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("count_mess_unread")]
        public int? CountMessUnread { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        /// <summary>
        /// The ID of the channel's creator.
        /// </summary>
        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("creator_name")]
        public string CreatorName { get; set; }

        [JsonPropertyName("e2ee")]
        public int? E2ee { get; set; }

        [JsonPropertyName("is_mute")]
        public bool? IsMute { get; set; }

        [JsonPropertyName("last_pin_message")]
        public string LastPinMessage { get; set; }

        [JsonPropertyName("last_seen_message")]
        public ChannelMessageHeaderResponse? LastSeenMessage { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ChannelMessageHeaderResponse? LastSentMessage { get; set; }

        [JsonPropertyName("meeting_code")]
        public string MeetingCode { get; set; }

        [JsonPropertyName("meeting_uri")]
        public string MeetingUri { get; set; }

        /// <summary>
        /// The parent channel this message belongs to.
        /// </summary>
        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("is_online")]
        public List<bool>? IsOnline { get; set; }

        [JsonPropertyName("topic")]
        public string Topic { get; set; }

        /// <summary>
        /// The channel type.
        /// </summary>
        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonPropertyName("user_id")]
        public List<string>? UserId { get; set; }

        [JsonPropertyName("usernames")]
        public List<string>? Usernames { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("metadata")]
        public List<string>? Metadata { get; set; }

        [JsonPropertyName("about_me")]
        public List<string>? AboutMe { get; set; }

        [JsonPropertyName("display_names")]
        public List<string>? DisplayNames { get; set; }

        [JsonPropertyName("app_id")]
        public string AppId { get; set; }
    }
}
