using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class CategoryDescriptionsResponse
    {
        [JsonPropertyName("categorydesc")]
        public List<CategoryDescriptionResponse>? CategoryDesc { get; set; }
    }
}
