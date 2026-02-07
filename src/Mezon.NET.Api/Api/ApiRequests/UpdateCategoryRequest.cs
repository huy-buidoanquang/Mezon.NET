using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UpdateCategoryRequest
    {
        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }
    }
}
