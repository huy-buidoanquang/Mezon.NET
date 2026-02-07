using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UpdateAccountRequest
    {
        [JsonProperty("about_me")]
        public string? AboutMe { get; set; }

        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("dob")]
        public string? Dob { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("encrypt_private_key")]
        public string? EncryptPrivateKey { get; set; }

        [JsonProperty("lang_tag")]
        public string? LangTag { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }

        [JsonProperty("logo")]
        public string? Logo { get; set; }

        [JsonProperty("splash_screen")]
        public string? SplashScreen { get; set; }

        [JsonProperty("timezone")]
        public string? Timezone { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
