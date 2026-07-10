using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Represents a list of clan sort by desc.
    /// </summary>
    public class ClansDescResponse
    {
        /// <summary>
        /// A list of clan sort by desc.
        /// </summary>
        [JsonProperty("clandesc")]
        public List<ClanDescResponse>? ClansDesc { get; set; }
    }
}
