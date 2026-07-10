using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class CategoryDescriptionsResponse
    {
        [JsonProperty("categorydesc")]
        public List<CategoryDescriptionResponse>? CategoryDesc { get; set; }
    }
}
