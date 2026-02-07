namespace Mezon.NET.DependencyInjection
{
    public class MezonSocketOptions
    {
        private const string DefaultScheme = "wss";
        private const int DefaultPort = 443;
        private const bool DefaultSSL = true;
        public const int DefaultHeartbeatTimeoutMs = 10000;
        public const int DefaultSendTimeoutMs = 10000;
        public const int DefaultConnectTimeoutMs = 30000;

        public string Scheme { get; set; } = DefaultScheme;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = DefaultPort;
        public bool UseSSL { get; set; } = DefaultSSL;
        public int HeartbeatTimeoutInMilliseconds { get; set; } = DefaultHeartbeatTimeoutMs;
        public int SendTimeoutInMilliseconds { get; set; } = DefaultSendTimeoutMs;
        public int ConnectTimeoutInMilliseconds { get; set; } = DefaultConnectTimeoutMs;
    }
}
