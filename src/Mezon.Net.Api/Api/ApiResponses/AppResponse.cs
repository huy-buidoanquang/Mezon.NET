using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class AppResponse
    {
        [JsonProperty("about")]
        public string? About { get; set; }

        [JsonProperty("app_url")]
        public string? AppUrl { get; set; }

        [JsonProperty("applogo")]
        public string? Applogo { get; set; }

        [JsonProperty("appname")]
        public string? Appname { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("disable_time")]
        public string? DisableTime { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonProperty("role")]
        public int? Role { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }
    }
}
