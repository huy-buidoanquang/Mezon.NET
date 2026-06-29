using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class CreateCategoryDescriptionRequest
    {
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }
}
