using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateCategoryOrdersRequest
    {
        [JsonPropertyName("categories")]
        public List<UpdateCategoryOrderRequest>? Categories { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }
}
