using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class RegistrationEmailRequest
    {
        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("dob")]
        public string? Dob { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("password")]
        public string? Password { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }
}
