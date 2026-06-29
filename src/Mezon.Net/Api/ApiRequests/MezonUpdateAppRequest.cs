using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class MezonUpdateAppRequest
    {
        [JsonPropertyName("about")]
        public string About { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("applogo")]
        public string Applogo { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
