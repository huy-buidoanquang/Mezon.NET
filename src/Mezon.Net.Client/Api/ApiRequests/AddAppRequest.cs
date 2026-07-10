using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AddAppRequest
    {
        [JsonProperty("about_me")]
        public string? AboutMe { get; set; }

        [JsonProperty("app_logo")]
        public string? AppLogo { get; set; }

        [JsonProperty("app_url")]
        public string? AppUrl { get; set; }

        [JsonProperty("appname")]
        public string? Appname { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonProperty("role")]
        public int? Role { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }
    }
}
