using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UserResponse
    {
        [JsonProperty("about_me")]
        public string? AboutMe { get; set; }

        [JsonProperty("apple_id")]
        public string? AppleId { get; set; }

        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("dob")]
        public string? Dob { get; set; }

        [JsonProperty("create_time")]
        public string? CreateTime { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("edge_count")]
        public int? EdgeCount { get; set; }

        [JsonProperty("facebook_id")]
        public string? FacebookId { get; set; }

        [JsonProperty("gamecenter_id")]
        public string? GamecenterId { get; set; }

        [JsonProperty("google_id")]
        public string? GoogleId { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("is_mobile")]
        public bool? IsMobile { get; set; }

        [JsonProperty("join_time")]
        public string? JoinTime { get; set; }

        [JsonProperty("lang_tag")]
        public string? LangTag { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }

        [JsonProperty("metadata")]
        public string? Metadata { get; set; }

        [JsonProperty("online")]
        public bool? Online { get; set; }

        [JsonProperty("steam_id")]
        public string? SteamId { get; set; }

        [JsonProperty("timezone")]
        public string? Timezone { get; set; }

        [JsonProperty("update_time")]
        public string? UpdateTime { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
