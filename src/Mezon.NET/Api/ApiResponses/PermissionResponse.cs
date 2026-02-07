using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class PermissionResponse
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("scope")]
        public int? Scope { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
}
