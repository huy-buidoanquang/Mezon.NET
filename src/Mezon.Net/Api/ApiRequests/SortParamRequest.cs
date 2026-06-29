using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class SortParamRequest
    {
        [JsonPropertyName("field_name")]
        public string FieldName { get; set; }

        [JsonPropertyName("order")]
        public string Order { get; set; }
    }
}
