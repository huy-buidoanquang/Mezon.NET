using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class CheckDuplicateClanNameResponse
    {
        [JsonProperty("is_duplicate")]
        public bool IsDuplicate { get; set; }
    }
}
