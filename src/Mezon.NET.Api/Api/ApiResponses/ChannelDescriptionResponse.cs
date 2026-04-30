using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    /// <summary>
    /// Represents a detailed description of a channel.
    /// </summary>
    public class ChannelDescriptionResponse
    {
        [JsonProperty("active")]
        public int? Active { get; set; }

        [JsonProperty("age_restricted")]
        public int? AgeRestricted { get; set; }

        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }

        [JsonProperty("channel_avatar")]
        public List<string>? ChannelAvatar { get; set; }

        /// <summary>
        /// The channel this message belongs to.
        /// </summary>
        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_label")]
        public string? ChannelLabel { get; set; }

        [JsonProperty("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_name")]
        public string? ClanName { get; set; }

        [JsonProperty("count_mess_unread")]
        public int? CountMessUnread { get; set; }

        [JsonProperty("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        /// <summary>
        /// The ID of the channel's creator.
        /// </summary>
        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("creator_name")]
        public string? CreatorName { get; set; }

        [JsonProperty("e2ee")]
        public int? E2ee { get; set; }

        [JsonProperty("is_mute")]
        public bool? IsMute { get; set; }

        [JsonProperty("last_pin_message")]
        public string? LastPinMessage { get; set; }

        [JsonProperty("last_seen_message")]
        public ChannelMessageHeaderResponse? LastSeenMessage { get; set; }

        [JsonProperty("last_sent_message")]
        public ChannelMessageHeaderResponse? LastSentMessage { get; set; }

        [JsonProperty("meeting_code")]
        public string? MeetingCode { get; set; }

        [JsonProperty("meeting_uri")]
        public string? MeetingUri { get; set; }

        /// <summary>
        /// The parent channel this message belongs to.
        /// </summary>
        [JsonProperty("parent_id")]
        public string? ParentId { get; set; }

        [JsonProperty("is_online")]
        public List<bool>? IsOnline { get; set; }

        [JsonProperty("topic")]
        public string? Topic { get; set; }

        /// <summary>
        /// The channel type.
        /// </summary>
        [JsonProperty("type")]
        public int? Type { get; set; }

        [JsonProperty("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonProperty("user_id")]
        public List<string>? UserId { get; set; }

        [JsonProperty("usernames")]
        public List<string>? Usernames { get; set; }

        [JsonProperty("status")]
        public int? Status { get; set; }

        [JsonProperty("metadata")]
        public List<string>? Metadata { get; set; }

        [JsonProperty("about_me")]
        public List<string>? AboutMe { get; set; }

        [JsonProperty("display_names")]
        public List<string>? DisplayNames { get; set; }

        [JsonProperty("app_id")]
        public string? AppId { get; set; }
    }
}
