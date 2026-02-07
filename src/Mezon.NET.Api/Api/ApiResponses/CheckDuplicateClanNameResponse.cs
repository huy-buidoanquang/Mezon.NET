using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class CheckDuplicateClanNameResponse
    {
        [JsonProperty("is_duplicate")]
        public bool IsDuplicate { get; set; }
    }
}
