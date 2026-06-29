using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateCategoryOrderRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("order")]
        public int? Order { get; set; }
    }
}
