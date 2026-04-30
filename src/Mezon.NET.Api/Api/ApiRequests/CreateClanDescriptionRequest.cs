using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class CreateClanDescriptionRequest
    {
        [JsonProperty("banner")]
        public string? Banner { get; set; }

        [JsonProperty("clan_name")]
        public string? ClanName { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("logo")]
        public string? Logo { get; set; }
    }
}
