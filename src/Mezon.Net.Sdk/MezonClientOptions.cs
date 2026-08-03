using Mezon.Net.Client;

namespace Mezon.Net.Sdk
{
    public sealed class MezonClientOptions : MezonSocketClientOptions
    {
        /// <summary>
        ///     Empty by default — agent SSE is opt-in. Set a reachable agent base URL when needed.
        /// </summary>
        public const string DefaultAgentEventUrl = "";

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
