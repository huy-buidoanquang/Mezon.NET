using Mezon.Net.Client;

namespace Mezon.Net.Sdk
{
    public sealed class MezonClientOptions : MezonSocketClientOptions
    {
        public const string DefaultAgentEventUrl = "http://172.16.110.19:8002";

        public long BotId { get; set; } = 0;
        public string Token { get; set; } = string.Empty;
        public string AgentEventUrl { get; set; } = DefaultAgentEventUrl;
        public int CacheCapacity { get; set; } = 512;

        public MezonClientOptions()
        {
        }

        public MezonClientOptions(long botId, string token, string host = DefaultHost, string port = DefaultPort, bool useSSL = DefaultUseSSL)
            : base(host, port, useSSL)
        {
            BotId = botId;
            Token = token;
        }
    }
}
