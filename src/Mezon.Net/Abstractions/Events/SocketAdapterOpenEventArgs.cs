using System.Net.WebSockets;

namespace Mezon.NET.Abstractions.Events
{
    public class SocketAdapterOpenEventArgs : SocketAdapterEventArgs
    {
        public SocketAdapterOpenEventArgs(ClientWebSocket target) : base("open", target)
        {
        }
    }
}
