using System;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Transport
{
    public static class DefaultNetworkTransportProvider
    {
        public static readonly MezonNetworkTransportProvider Instance = Create();

        /// <summary>
        /// Creates a provider that picks TCP/WebSocket from the <paramref name="type"/>
        /// passed at each call (after <see cref="TransportTypeExtensions.Resolve"/>).
        /// </summary>
        public static MezonNetworkTransportProvider Create(TransportType type = TransportType.Auto)
        {
            if (type == TransportType.Auto)
            {
                return requested => CreateTransporter(requested.Resolve());
            }

            var fixedType = type.Resolve();
            return _ => CreateTransporter(fixedType);
        }

        private static IMezonNetworkTransporter CreateTransporter(TransportType resolved)
        {
            try
            {
                return resolved switch
                {
                    TransportType.Tcp => new MezonNetworkTcpTransporter(),
                    TransportType.WebSocket => new MezonNetworkWebSocketTransporter(),
                    _ => throw new PlatformNotSupportedException(
                        $"Transport type '{resolved}' is not supported."),
                };
            }
            catch (PlatformNotSupportedException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not PlatformNotSupportedException)
            {
                throw new PlatformNotSupportedException(
                    $"The default MezonNetwork transporter for '{resolved}' is not supported on this platform.",
                    ex);
            }
        }
    }
}
