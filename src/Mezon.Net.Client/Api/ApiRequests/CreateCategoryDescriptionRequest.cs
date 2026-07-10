using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class CreateCategoryDescriptionRequest
    {
        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }
    }
}
