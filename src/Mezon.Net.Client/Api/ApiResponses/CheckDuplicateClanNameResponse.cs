using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class CheckDuplicateClanNameResponse
    {
        [JsonProperty("is_duplicate")]
        public bool IsDuplicate { get; set; }
    }
}
