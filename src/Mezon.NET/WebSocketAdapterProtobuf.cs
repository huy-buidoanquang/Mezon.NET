using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Events;

namespace Mezon.NET
{
    internal class WebSocketAdapterProtobuf : IWebSocketAdapter
    {
        public event EventHandler<SocketAdapterOpenEventArgs>? Opened;
        public event EventHandler<SocketAdapterCloseEventArgs>? Closed;
        public event EventHandler<SocketAdapterMessageEventArgs>? MessageReceived;
        public event EventHandler<SocketAdapterErrorEventArgs>? ErrorOccurred;

        public Task CloseAsync(CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task ConnectAsync(string scheme, string host, int port, bool createStatus, string token, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public bool IsOpen()
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(object message, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }
    }
}
