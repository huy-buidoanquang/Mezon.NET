using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class UpdateCategoryOrdersRequest
    {
        [JsonProperty("categories")]
        public List<UpdateCategoryOrderRequest>? Categories { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }
    }
}
