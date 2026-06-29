using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ClanDescriptionProfileResponse
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("nick_name")]
        public string NickName { get; set; }

        [JsonPropertyName("profile_banner")]
        public string ProfileBanner { get; set; }

        [JsonPropertyName("profile_theme")]
        public string ProfileTheme { get; set; }
    }
}
