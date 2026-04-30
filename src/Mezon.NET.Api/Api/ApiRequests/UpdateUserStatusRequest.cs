using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class UpdateUserStatusRequest
    {
        [JsonProperty("minutes")]
        public int? Minutes { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("until_turn_on")]
        public bool? UntilTurnOn { get; set; }
    }
}
