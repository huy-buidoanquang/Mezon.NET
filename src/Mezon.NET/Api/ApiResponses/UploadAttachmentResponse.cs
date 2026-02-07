using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UploadAttachmentResponse
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
