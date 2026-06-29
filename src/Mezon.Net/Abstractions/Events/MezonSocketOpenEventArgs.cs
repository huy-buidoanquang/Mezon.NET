using System.Net.WebSockets;

namespace Mezon.NET.Abstractions.Events
{
    public class MezonSocketOpenEventArgs : SocketAdapterOpenEventArgs
    {
        public MezonSocketOpenEventArgs(ClientWebSocket target) : base(target)
        {
        }
    }
}
