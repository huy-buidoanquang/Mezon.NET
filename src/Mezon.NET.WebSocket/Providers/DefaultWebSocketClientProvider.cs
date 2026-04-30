using System;
using System.Net;
using Mezon.Net.Abstractions;

namespace Mezon.Net.WebSocket.Providers
{
    public static class DefaultWebSocketClientProvider
    {
        public static readonly WebSocketClientProvider Instance = Create();

        /// <exception cref="PlatformNotSupportedException">The default WebSocketClientProvider is not supported on this platform.</exception>
        public static WebSocketClientProvider Create(IWebProxy? webProxy = null)
        {
            return () =>
            {
                try
                {
                    return new DefaultWebSocketClient(webProxy);
                }
                catch (PlatformNotSupportedException ex)
                {
                    throw new PlatformNotSupportedException("The default WebSocketClientProvider is not supported on this platform.", ex);
                }
            };
        }
    }
}
