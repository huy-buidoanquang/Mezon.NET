using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class RoleUserResponse
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("online")]
        public bool? Online { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
