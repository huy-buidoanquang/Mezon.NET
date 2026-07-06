using System;
using Mezon.Net.Core;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Transport
{
    public static class DefaultNetworkTransportProvider
    {
        public static readonly MezonNetworkTransportProvider Instance = Create();

        public static MezonNetworkTransportProvider Create(TransportType type = TransportType.Auto)
        {

            var targetType = type.Resolve();

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
                            throw new PlatformNotSupportedException("The default MezonNetworkTcpTransporter is not supported on this platform.", ex);
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
                            throw new PlatformNotSupportedException("The default MezonNetworkWebSocketTransporter is not supported on this platform.", ex);
                        }
                    };
                default:
                    throw new PlatformNotSupportedException("The default MezonNetworkTransporter is not supported on this platform.");
            }
        }
    }
}
