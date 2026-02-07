using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UserResponse
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("apple_id")]
        public string AppleId { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("dob")]
        public string Dob { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("edge_count")]
        public int? EdgeCount { get; set; }

        [JsonPropertyName("facebook_id")]
        public string FacebookId { get; set; }

        [JsonPropertyName("gamecenter_id")]
        public string GamecenterId { get; set; }

        [JsonPropertyName("google_id")]
        public string GoogleId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_mobile")]
        public bool? IsMobile { get; set; }

        [JsonPropertyName("join_time")]
        public string JoinTime { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; }

        [JsonPropertyName("online")]
        public bool? Online { get; set; }

        [JsonPropertyName("steam_id")]
        public string SteamId { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
