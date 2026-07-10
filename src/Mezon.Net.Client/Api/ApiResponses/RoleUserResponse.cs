using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class RoleUserResponse
    {
        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("lang_tag")]
        public string? LangTag { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }

        [JsonProperty("online")]
        public bool? Online { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
