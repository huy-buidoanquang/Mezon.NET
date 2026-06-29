using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class AddAppRequest
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("app_logo")]
        public string AppLogo { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonPropertyName("role")]
        public int? Role { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
