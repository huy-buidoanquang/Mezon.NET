using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class CreateClanDescriptionRequest
    {
        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }
    }
}
