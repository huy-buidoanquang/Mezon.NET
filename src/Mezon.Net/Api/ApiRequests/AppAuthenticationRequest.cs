using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class AppAuthenticationRequest
    {
        [JsonPropertyName("account")]
        public AppAccountRequest AppAccount { get; set; }

        public AppAuthenticationRequest(AppAccountRequest appAccount)
        {
            AppAccount = appAccount;
        }
    }
}
