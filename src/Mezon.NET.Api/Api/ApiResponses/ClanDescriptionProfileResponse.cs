using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ClanDescriptionProfileResponse
    {
        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("nick_name")]
        public string? NickName { get; set; }

        [JsonProperty("profile_banner")]
        public string? ProfileBanner { get; set; }

        [JsonProperty("profile_theme")]
        public string? ProfileTheme { get; set; }
    }
}
