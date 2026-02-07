using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateAccountRequest
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("dob")]
        public string Dob { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("encrypt_private_key")]
        public string EncryptPrivateKey { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("splash_screen")]
        public string SplashScreen { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
