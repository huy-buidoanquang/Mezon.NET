using System;
using System.Runtime.InteropServices;
using Mezon.Net.Core;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Transport
{
    public static class DefaultNetworkTransportProvider
    {
        public static readonly MezonNetworkTransportProvider Instance = Create();

        public static MezonNetworkTransportProvider Create(TransportType type = TransportType.Auto)
        {

            var targetType = type;

            if (type == TransportType.Auto)
            {
                targetType = GetRecommendedTransport();
            }

            switch (targetType)
            {
                case TransportType.Tcp:
                    return (type) =>
                    {
                        try
                        {
                            return new MezonNetworkTcpTransporter();
                        }
                        catch (PlatformNotSupportedException ex)
                        {
                            throw new PlatformNotSupportedException("The default TCPClientProvider is not supported on this platform.", ex);
                        }
                    };
                case TransportType.WebSocket:
                    return (type) =>
                    {
                        try
                        {
                            return new MezonNetworkWebSocketTransporter();
                        }
                        catch (PlatformNotSupportedException ex)
                        {
                            throw new PlatformNotSupportedException("The default TCPClientProvider is not supported on this platform.", ex);
                        }
                    };
                default:
                    throw new PlatformNotSupportedException("The default TCPClientProvider is not supported on this platform.");
            }
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
