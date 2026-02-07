using Mezon.NET.Core;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    internal class MezonErrorResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
        [JsonProperty("code")]
        public MezonErrorCode Code { get; set; }
        [JsonProperty("errors")]
        public Optional<ErrorDetails[]> Errors { get; set; }
    }

    internal class ErrorDetails
    {
        [JsonProperty("name")]
        public Optional<string> Name { get; set; }
        [JsonProperty("errors")]
        public Optional<Error[]> Errors { get; set; }
    }

    internal class Error
    {
        [JsonProperty("code")]
        public string? Code { get; set; }
        [JsonProperty("message")]
        public string? Message { get; set; }
    }
}
