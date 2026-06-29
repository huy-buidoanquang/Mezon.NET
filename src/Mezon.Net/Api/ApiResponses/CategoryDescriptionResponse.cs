using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class CategoryDescriptionResponse
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("category_order")]
        public int? CategoryOrder { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }
    }
}
