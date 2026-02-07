using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class AccountResponse
    {
        [JsonPropertyName("custom_id")]
        public string CustomId { get; set; }

        [JsonPropertyName("disable_time")]
        public string DisableTime { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("encrypt_private_key")]
        public string EncryptPrivateKey { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("splash_screen")]
        public string SplashScreen { get; set; }

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }

        [JsonPropertyName("verify_time")]
        public string VerifyTime { get; set; }

        [JsonPropertyName("wallet")]
        public double? Wallet { get; set; }
    }
}
