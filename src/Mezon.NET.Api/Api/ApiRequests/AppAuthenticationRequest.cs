using Newtonsoft.Json;

namespace Mezon.NET.Api
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
