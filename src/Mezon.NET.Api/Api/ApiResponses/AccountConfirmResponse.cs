using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class AccountConfirmResponse
    {
        [JsonProperty("otp_code")]
        public string? OTP { get; set; }

        [JsonProperty("req_id")]
        public string? RequestId { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }
    }
}
