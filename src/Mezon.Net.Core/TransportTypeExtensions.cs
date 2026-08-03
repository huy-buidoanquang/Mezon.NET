using System.Runtime.InteropServices;

namespace Mezon.Net.Core
{
    public static class TransportTypeExtensions
    {
        public static TransportType Resolve(this TransportType transportType)
        {
            if (transportType != TransportType.Auto)
            {
                return transportType;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            {
                return TransportType.WebSocket;
            }

            return TransportType.Tcp;
        }
    }
}
