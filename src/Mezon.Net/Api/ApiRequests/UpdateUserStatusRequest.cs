using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateUserStatusRequest
    {
        [JsonPropertyName("minutes")]
        public int? Minutes { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("until_turn_on")]
        public bool? UntilTurnOn { get; set; }
    }
}
