using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using Mezon.Net.Core;
using Mezon.Net.Transport.Tcp;
using Mezon.Net.Transport.WebSocket;
using Microsoft.Win32;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Transport
{
    public static class DefaultNetworkTransportProvider
    {
        public static readonly MezonNetworkTransportProvider Instance = Create(TransportType.Auto);
        private static ConcurrentDictionary<TransportType, MezonNetworkTransportProvider> _registry = new ConcurrentDictionary<TransportType, MezonNetworkTransportProvider>();

        /// <summary>
        /// Các gói Transport cụ thể (Tcp, WS) sẽ gọi hàm này để "báo danh" với Core
        /// </summary>
        public static void Register(TransportType type, MezonNetworkTransportProvider provider)
        {
            _registry[type] = provider;
        }

        public static MezonNetworkTransportProvider Create(TransportType type = TransportType.Auto)
        {
            _registry = new ConcurrentDictionary<TransportType, MezonNetworkTransportProvider>();
            Register(TransportType.Tcp, () => new MezonNetworkTcpTransporter());
            Register(TransportType.WebSocket, () => new MezonNetworkWebSocketTransporter());
            var targetType = type;

            if (type == TransportType.Auto)
            {
                targetType = GetRecommendedTransport();
            }

            if (_registry.TryGetValue(targetType, out var provider))
            {
                return () =>
                {
                    try
                    {
                        return provider();
                    }
                    catch (PlatformNotSupportedException ex)
                    {
                        throw new PlatformNotSupportedException("The default WebSocketClientProvider is not supported on this platform.", ex);
                    }
                };
            }

            throw new InvalidOperationException(
                $"Transport {targetType} chưa được đăng ký. " +
                $"Hãy đảm bảo bạn đã cài đặt gói Mezon.Net.Transport.{targetType} và gọi hàm Register.");
        }

        private static TransportType GetRecommendedTransport()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            {
                return TransportType.WebSocket;
            }
            return TransportType.Tcp;
        }
    }
}
