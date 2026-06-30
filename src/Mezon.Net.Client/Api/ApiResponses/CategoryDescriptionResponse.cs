using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class CategoryDescriptionResponse
    {
        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }

        [JsonProperty("category_order")]
        public int? CategoryOrder { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }
    }
}
