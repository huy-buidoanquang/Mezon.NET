using Mezon.Net.Core.Abstractions;

namespace Mezon.Net.Transport
{
    public static class MezonNetworkTransporterExtensions
    {
        public static void ResetApiStream(this IMezonNetworkTransporter transporter, int cid)
        {
            switch (transporter)
            {
                case MezonNetworkTcpTransporter tcp:
                    tcp.ResetApiStream(cid);
                    break;
                case MezonNetworkWebSocketTransporter ws:
                    ws.ResetApiStream(cid);
                    break;
            }
        }
    }
}
