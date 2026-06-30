using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class CategoryDescriptionsResponse
    {
        [JsonProperty("categorydesc")]
        public List<CategoryDescriptionResponse>? CategoryDesc { get; set; }
    }
}
