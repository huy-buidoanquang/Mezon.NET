using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class FilterParamRequest
    {
        [JsonPropertyName("field_name")]
        public string FieldName { get; set; }

        [JsonPropertyName("field_value")]
        public string FieldValue { get; set; }
    }
}
