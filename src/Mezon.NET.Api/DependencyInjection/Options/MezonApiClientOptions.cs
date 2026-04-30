namespace Mezon.Net.DependencyInjection
{
    public class MezonApiClientOptions
    {
        private const string DefaultHost = "gw.mezon.ai";
        private const int DefaultPort = 443;
        private const int DefaultTimeoutInMilliseconds = 7000;
        private const bool DefaultSSL = true;

        public string Host { get; set; } = DefaultHost;
        public int Port { get; set; } = DefaultPort;
        public bool UseSSL { get; set; } = DefaultSSL;
        public string GatewayBasePath { get; set; } = string.Empty;
        public string ApiBasePath { get; set; } = string.Empty;
        public int TimeoutInMilliseconds { get; set; } = DefaultTimeoutInMilliseconds;
    }
}
