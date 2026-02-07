using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UploadAttachmentResponse
    {
        [JsonProperty("filename")]
        public string? Filename { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }
}
