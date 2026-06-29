using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class UploadAttachmentRequest
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("filetype")]
        public string Filetype { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }
    }
}
