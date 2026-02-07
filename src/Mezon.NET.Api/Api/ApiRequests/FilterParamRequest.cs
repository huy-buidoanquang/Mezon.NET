using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class FilterParamRequest
    {
        [JsonProperty("field_name")]
        public string? FieldName { get; set; }

        [JsonProperty("field_value")]
        public string? FieldValue { get; set; }
    }
}
