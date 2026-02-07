using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents the description of a clan.
    /// </summary>
    public class ClanDescriptionResponse
    {
        /// <summary>
        /// The URL for the clan's banner image.
        /// </summary>
        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        /// <summary>
        /// The unique identifier for the clan.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The name of the clan.
        /// </summary>
        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        /// <summary>
        /// The user ID of the clan's creator.
        /// </summary>
        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        /// <summary>
        /// The URL for the clan's logo image.
        /// </summary>
        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        /// <summary>
        /// The status of the clan.
        /// </summary>
        [JsonPropertyName("status")]
        public int? Status { get; set; }

        /// <summary>
        /// The count of badges for the clan.
        /// </summary>
        [JsonPropertyName("badge_count")]
        public int? BadgeCount { get; set; }

        /// <summary>
        /// Indicates if the clan is in an onboarding state.
        /// </summary>
        [JsonPropertyName("is_onboarding")]
        public bool? IsOnboarding { get; set; }

        /// <summary>
        /// The ID of the clan's welcome channel.
        /// </summary>
        [JsonPropertyName("welcome_channel_id")]
        public string WelcomeChannelId { get; set; }

        /// <summary>
        /// The URL for the clan's onboarding banner image.
        /// </summary>
        [JsonPropertyName("onboarding_banner")]
        public string OnboardingBanner { get; set; }
    }
}
