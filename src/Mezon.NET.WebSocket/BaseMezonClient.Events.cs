using System;
using System.Threading.Tasks;
using Mezon.NET.Core;

namespace Mezon.NET.WebSocket
{
    public partial class BaseMezonClient
    {
        public event Func<SocketChannelMessage, Task> ChannelMessageReceived
        {
            add { _channelMessageReceivedEvent.Add(value); }
            remove { _channelMessageReceivedEvent.Remove(value); }
        }

        internal readonly AsyncEvent<Func<SocketChannelMessage, Task>> _channelMessageReceivedEvent = new AsyncEvent<Func<SocketChannelMessage, Task>>();
    }
}
