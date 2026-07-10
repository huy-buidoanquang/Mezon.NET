using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UpdateCategoryOrderRequest
    {
        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("order")]
        public int? Order { get; set; }
    }
}
