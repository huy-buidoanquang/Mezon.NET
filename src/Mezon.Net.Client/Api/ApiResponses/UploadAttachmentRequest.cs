using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class UploadAttachmentRequest
    {
        [JsonProperty("filename")]
        public string? Filename { get; set; }

        [JsonProperty("filetype")]
        public string? Filetype { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonProperty("size")]
        public int? Size { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }
    }
}
