using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class AppAuthenticationRequest
    {
        [JsonProperty("account")]
        public AppAccountRequest AppAccount { get; set; }

        public AppAuthenticationRequest(AppAccountRequest appAccount)
        {
            AppAccount = appAccount;
        }
    }
}
