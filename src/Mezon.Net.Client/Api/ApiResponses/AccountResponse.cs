using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AccountResponse
    {
        [JsonProperty("custom_id")]
        public string? CustomId { get; set; }

        [JsonProperty("disable_time")]
        public string? DisableTime { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("encrypt_private_key")]
        public string? EncryptPrivateKey { get; set; }

        [JsonProperty("logo")]
        public string? Logo { get; set; }

        [JsonProperty("splash_screen")]
        public string? SplashScreen { get; set; }

        [JsonProperty("user")]
        public UserResponse? User { get; set; }

        [JsonProperty("verify_time")]
        public string? VerifyTime { get; set; }

        [JsonProperty("wallet")]
        public double? Wallet { get; set; }
    }
}
