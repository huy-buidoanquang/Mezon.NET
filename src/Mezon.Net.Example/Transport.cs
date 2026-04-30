using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mezon.Net.Transport.Tcp;
using Mezon.Net.Transport.WebSocket;
using static Mezon.Net.Core.Abstractions.IMezonNetworkTransporter;

namespace Mezon.Net.Example;

public static class Transport
{
    public static async Task Run()
    {
        var transporter = Mezon.Net.Transport.DefaultNetworkTransportProvider.Instance();
        await transporter.ConnectAsync("dev-mezon.nccsoft.vn", 8080, "AAASNNPnPryvw_SGRdU30RYsGGVqKSDrIWrHkGUuihAb77nf");
    }
}
