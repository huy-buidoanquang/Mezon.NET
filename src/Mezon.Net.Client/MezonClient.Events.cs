using System;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Client
{
    public partial class MezonClient
    {
        public event Func<Task> Connected
        {
            add { _connectedEvent.Add(value); }
            remove { _connectedEvent.Remove(value); }
        }
        private readonly AsyncEvent<Func<Task>> _connectedEvent = new AsyncEvent<Func<Task>>();

        /// <summary>
        /// Raised after the socket session has been torn down.
        /// </summary>
        public event Func<Exception, Task> Disconnected
        {
            add { _disconnectedEvent.Add(value); }
            remove { _disconnectedEvent.Remove(value); }
        }
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();

        /// <summary>
        /// Raised after <see cref="Disconnected"/> when the connection manager will attempt to
        /// reconnect automatically (unexpected drop, not user disconnect or session invalidation).
        /// </summary>
        public event Func<Exception, Task> Reconnecting
        {
            add { _reconnectingEvent.Add(value); }
            remove { _reconnectingEvent.Remove(value); }
        }
        private readonly AsyncEvent<Func<Exception, Task>> _reconnectingEvent = new AsyncEvent<Func<Exception, Task>>();

    }
}
