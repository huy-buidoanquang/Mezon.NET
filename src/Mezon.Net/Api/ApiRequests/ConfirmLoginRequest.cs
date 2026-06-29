using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class ConfirmLoginRequest
    {
        [JsonPropertyName("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonPropertyName("login_id")]
        public string LoginId { get; set; }
    }
}
