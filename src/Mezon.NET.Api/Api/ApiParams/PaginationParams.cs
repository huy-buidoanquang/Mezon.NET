using Mezon.NET.Core;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class PaginationParams
    {
        [JsonProperty("limit")]
        public Optional<int> Limit { get; set; }
        [JsonProperty("state")]
        public Optional<int> State { get; set; }
        [JsonProperty("cursor")]
        public Optional<string> Cursor { get; set; }
    }
}
