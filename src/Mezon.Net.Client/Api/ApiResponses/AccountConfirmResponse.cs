using Newtonsoft.Json;

namespace Mezon.Net.Client
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
