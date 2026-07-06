using Mezon.Net.Client;
using Mezon.Net.Core;

namespace Mezon.Net.Sdk
{
    public sealed class MezonClientOptions : MezonOptions
    {
        public const string DefaultMmnApiUrl = "https://dong.mezon.ai/mmn-api/";
        public const string DefaultZkApiUrl = "https://dong.mezon.ai/zk-api/";
        public const string DefaultAgentEventUrl = "http://172.16.110.19:8002/";

        public string BotId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public int RequestTimeoutMs { get; set; } = 7000;
        public string MmnApiUrl { get; set; } = DefaultMmnApiUrl;
        public string AgentEventUrl { get; set; } = DefaultAgentEventUrl;
        public int CacheCapacity { get; set; } = 512;

        public MezonClientOptions()
        {
        }

        public MezonClientOptions(string botId, string token, string host = DefaultHost, string port = DefaultPort, bool useSSL = DefaultUseSSL)
            : base(host, port, useSSL)
        {
            BotId = botId;
            Token = token;
        }

        public MezonSocketClientOptions ToSocketOptions()
        {
            return new MezonSocketClientOptions(Host, Port, UseSSL)
            {
                AutoRefreshSession = true,
            };
        }
    }
}
