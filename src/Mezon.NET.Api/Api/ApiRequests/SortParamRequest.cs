using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class SortParamRequest
    {
        [JsonProperty("field_name")]
        public string? FieldName { get; set; }

        [JsonProperty("order")]
        public string? Order { get; set; }
    }
}
