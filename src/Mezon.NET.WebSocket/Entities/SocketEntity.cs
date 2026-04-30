using System;
using Mezon.Net.Core;

namespace Mezon.Net.WebSocket
{
    public abstract class SocketEntity<T> : IEntity<T>
        where T : IEquatable<T>
    {
        internal MezonClient Socket { get; }

        /// <inheritdoc />
        public T Id { get; }

        internal SocketEntity(MezonClient socket, T id)
        {
            Socket = socket;
            Id = id;
        }
    }
}
