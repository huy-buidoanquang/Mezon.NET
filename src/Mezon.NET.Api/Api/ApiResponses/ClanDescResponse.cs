using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    /// <summary>
    /// Represents the description of a clan.
    /// </summary>
    public class ClanDescResponse
    {
        /// <summary>
        /// The URL for the clan's banner image.
        /// </summary>
        [JsonProperty("banner")]
        public string? Banner { get; set; }

        /// <summary>
        /// The unique identifier for the clan.
        /// </summary>
        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        /// <summary>
        /// The name of the clan.
        /// </summary>
        [JsonProperty("clan_name")]
        public string? ClanName { get; set; }

        /// <summary>
        /// The user ID of the clan's creator.
        /// </summary>
        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        /// <summary>
        /// The URL for the clan's logo image.
        /// </summary>
        [JsonProperty("logo")]
        public string? Logo { get; set; }

        /// <summary>
        /// The status of the clan.
        /// </summary>
        [JsonProperty("status")]
        public int? Status { get; set; }

        /// <summary>
        /// The count of badges for the clan.
        /// </summary>
        [JsonProperty("badge_count")]
        public int? BadgeCount { get; set; }

        /// <summary>
        /// Indicates if the clan is in an onboarding state.
        /// </summary>
        [JsonProperty("is_onboarding")]
        public bool? IsOnboarding { get; set; }

        /// <summary>
        /// The ID of the clan's welcome channel.
        /// </summary>
        [JsonProperty("welcome_channel_id")]
        public string? WelcomeChannelId { get; set; }

        /// <summary>
        /// The URL for the clan's onboarding banner image.
        /// </summary>
        [JsonProperty("onboarding_banner")]
        public string? OnboardingBanner { get; set; }
    }
}
