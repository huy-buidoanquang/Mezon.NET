using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateCategoryRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }
    }
}
