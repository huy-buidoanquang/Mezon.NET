using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class MezonUpdateAppRequest
    {
        [JsonProperty("about")]
        public string? About { get; set; }

        [JsonProperty("app_url")]
        public string? AppUrl { get; set; }

        [JsonProperty("applogo")]
        public string? Applogo { get; set; }

        [JsonProperty("appname")]
        public string? Appname { get; set; }

        [JsonProperty("metadata")]
        public string? Metadata { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }
    }
}
