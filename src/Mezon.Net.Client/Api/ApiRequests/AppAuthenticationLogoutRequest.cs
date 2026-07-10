using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AppAuthenticationLogoutRequest
    {
        [JsonProperty("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("device_id")]
        public string? DeviceId { get; set; }

        [JsonProperty("platform")]
        public string? Platform { get; set; }
    }
}
