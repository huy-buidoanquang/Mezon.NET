using System;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class LoginIDResponse
    {
        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("create_time_second")]
        public long? CreateTimeSecond { get; set; }

        [JsonProperty("login_id")]
        public long LoginId { get; set; } = 0;

        [JsonProperty("platform")]
        public string? Platform { get; set; }

        [JsonProperty("status")]
        public int? Status { get; set; }

        [JsonProperty("user_id")]
        public long? UserId { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }
}
