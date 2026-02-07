using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class PermissionResponse
    {
        [JsonProperty("active")]
        public int? Active { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("level")]
        public int? Level { get; set; }

        [JsonProperty("scope")]
        public int? Scope { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }
    }
}
