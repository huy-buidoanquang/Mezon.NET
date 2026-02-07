using System;
using Mezon.NET.Abstractions.Events;

namespace Mezon.NET.Abstractions
{
    public interface ISocketS2C
    {
        /// <summary>
        /// Dispatched when the web socket connection is successfully opened.
        /// </summary>
        event Action<MezonSocketOpenEventArgs>? Connected;

        /// <summary>
        /// Dispatched when the web socket connection closes.
        /// </summary>
        event Action<SocketAdapterCloseEventArgs>? Disconnected;

        /// <summary>
        /// Dispatched when the web socket encounters an error.
        /// </summary>
        event Action<SocketAdapterErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Dispatched when the web socket receives a notification message.
        /// </summary>
        event Action<NotificationEventArgs>? NotificationReceived;

        event Action<MessageTypingEventArgs>? MessageTyping;
    }
}
