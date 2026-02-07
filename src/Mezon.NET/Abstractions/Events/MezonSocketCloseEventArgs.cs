using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace Mezon.NET.Abstractions.Events
{
    public class MezonSocketCloseEventArgs : SocketAdapterCloseEventArgs
    {
        public MezonSocketCloseEventArgs(bool wasClean, int code, string reason, ClientWebSocket target) : base(wasClean, code, reason, target)
        {
        }
    }
}
