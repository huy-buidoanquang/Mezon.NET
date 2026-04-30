using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class ConfirmLoginRequest
    {
        [JsonProperty("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonProperty("login_id")]
        public long LoginId { get; set; }
    }
}
