using Mezon.NET.Api;
using Mezon.NET.Abstractions;
using Mezon.NET.WebSocket.Providers;

namespace Mezon.NET.WebSocket
{
    public class MezonSocketClientConfiguration : MezonApiClientConfiguration
    {
        public MezonSocketClientConfiguration()
        {
        }

        public MezonSocketClientConfiguration(string host, string port, bool useSSL) : base(host, port, useSSL)
        {
        }
        /// <summary> Gets or sets the provider used to generate new gRPC connections. </summary>
        public WebSocketClientProvider WebSocketClientProvider { get; set; } = DefaultWebSocketClientProvider.Instance;
    }
}
