using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    internal class RatelimitResponse
    {
        [JsonProperty("global")]
        public bool Global { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("retry_after")]
        public double RetryAfter { get; set; }
    }
}
