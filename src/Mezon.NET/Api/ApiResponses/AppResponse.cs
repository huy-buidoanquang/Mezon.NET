using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class AppResponse
    {
        [JsonPropertyName("about")]
        public string About { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("applogo")]
        public string Applogo { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("disable_time")]
        public string DisableTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonPropertyName("role")]
        public int? Role { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
