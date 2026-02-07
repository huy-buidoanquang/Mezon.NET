using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents a list of clan descriptions.
    /// </summary>
    public class ClanDescriptionsResponse
    {
        /// <summary>
        /// A list of clan descriptions.
        /// </summary>
        [JsonPropertyName("clandesc")]
        public List<ClanDescriptionResponse>? ClanDescriptions { get; set; }
    }
}
