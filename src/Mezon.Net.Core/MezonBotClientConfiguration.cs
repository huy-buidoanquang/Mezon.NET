namespace Mezon.Net.Core
{
    public class MezonBotClientConfiguration : MezonOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public MezonBotClientConfiguration(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        public MezonBotClientConfiguration(string clientId, string clientSecret, string host = DefaultHost, string port = DefaultPort, bool useSSL = DefaultUseSSL) : base(host, port, useSSL)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
        }
    }
}
