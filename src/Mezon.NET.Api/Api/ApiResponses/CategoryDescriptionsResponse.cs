using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class CategoryDescriptionsResponse
    {
        [JsonProperty("categorydesc")]
        public List<CategoryDescriptionResponse>? CategoryDesc { get; set; }
    }
}
