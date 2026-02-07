using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class CheckDuplicateClanNameResponse
    {
        [JsonPropertyName("is_duplicate")]
        public bool IsDuplicate { get; set; }
    }
}
