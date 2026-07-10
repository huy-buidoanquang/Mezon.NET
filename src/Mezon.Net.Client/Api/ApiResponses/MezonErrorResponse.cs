using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    internal class MezonErrorResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
    }
}
