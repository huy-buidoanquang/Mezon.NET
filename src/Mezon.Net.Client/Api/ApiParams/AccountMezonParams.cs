using Mezon.Net.Core;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class AccountMezonParams
    {
        [JsonProperty("create")]
        public Optional<bool> Create { get; set; }

        [JsonProperty("is_remember")]
        public Optional<bool> IsRemember { get; set; }

        [JsonProperty("username")]
        public Optional<string> Username { get; set; }
    }
}
